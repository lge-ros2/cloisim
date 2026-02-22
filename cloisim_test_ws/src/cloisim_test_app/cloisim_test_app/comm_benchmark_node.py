#!/usr/bin/env python3
"""
CLOiSim Communication Benchmark Node

Benchmarks ROS2 communication performance for two transport modes:
  1. ZMQ Legacy  : CLOiSim -> ZMQ -> cloisim_ros bridge -> ROS2 DDS
  2. ROS Native  : CLOiSim -> ROS2 DDS (direct, no bridge)

Measures per-topic: frequency, latency, jitter, bandwidth, message count.
Also records system-level CPU usage.

Usage:
  ros2 run cloisim_test_app comm_benchmark \
      --ros-args -p mode:=native -p robot_name:=BenchmarkBot -p duration:=30
"""

import math
import os
import time
import json
import statistics

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, HistoryPolicy
from rclpy.serialization import serialize_message

import psutil

from sensor_msgs.msg import Imu, LaserScan, Image, PointCloud2
from nav_msgs.msg import Odometry


# ------------------------------------------------------------------ constants
BEST_EFFORT_QOS = QoSProfile(
    reliability=ReliabilityPolicy.BEST_EFFORT,
    history=HistoryPolicy.KEEP_LAST,
    depth=10,
)

RELIABLE_QOS = QoSProfile(
    reliability=ReliabilityPolicy.RELIABLE,
    history=HistoryPolicy.KEEP_LAST,
    depth=10,
)

# Sensor definitions: (suffix, msg_type, qos)
SENSOR_DEFS = {
    "IMU":          ("imu",                    Imu,          BEST_EFFORT_QOS),
    "Odometry":     ("odom",                   Odometry,     RELIABLE_QOS),
    "2D_LiDAR":     ("scan",                   LaserScan,    BEST_EFFORT_QOS),
    "3D_LiDAR":     ("scan_3d",                PointCloud2,  BEST_EFFORT_QOS),
    "RGB_Camera":   ("camera/image_raw",       Image,        BEST_EFFORT_QOS),
    "Depth_Camera": ("depth_camera/image_raw", Image,        BEST_EFFORT_QOS),
}


# -------------------------------------------------------------- helper class
class _TopicStats:
    """Accumulates per-topic statistics."""

    __slots__ = (
        "count",
        "bytes_total",
        "latencies_ms",
        "recv_timestamps",
    )

    def __init__(self):
        self.count: int = 0
        self.bytes_total: int = 0
        self.latencies_ms: list[float] = []
        self.recv_timestamps: list[float] = []

    def reset(self):
        self.count = 0
        self.bytes_total = 0
        self.latencies_ms.clear()
        self.recv_timestamps.clear()

    # ---- derived metrics ----
    def hz(self, duration: float) -> float:
        return self.count / duration if duration > 0 else 0.0

    def bandwidth_mb_s(self, duration: float) -> float:
        return (self.bytes_total / duration) / (1024 * 1024) if duration > 0 else 0.0

    def latency_stats(self) -> dict:
        lats = self.latencies_ms
        if not lats:
            return {"avg_ms": 0.0, "min_ms": 0.0, "max_ms": 0.0,
                    "median_ms": 0.0, "p95_ms": 0.0, "p99_ms": 0.0,
                    "stddev_ms": 0.0}
        lats_sorted = sorted(lats)
        p95_idx = int(len(lats_sorted) * 0.95)
        p99_idx = int(len(lats_sorted) * 0.99)
        return {
            "avg_ms":    round(statistics.mean(lats), 3),
            "min_ms":    round(min(lats), 3),
            "max_ms":    round(max(lats), 3),
            "median_ms": round(statistics.median(lats), 3),
            "p95_ms":    round(lats_sorted[min(p95_idx, len(lats_sorted) - 1)], 3),
            "p99_ms":    round(lats_sorted[min(p99_idx, len(lats_sorted) - 1)], 3),
            "stddev_ms": round(statistics.stdev(lats), 3) if len(lats) > 1 else 0.0,
        }

    def jitter_ms(self) -> float:
        """Inter-arrival jitter (stddev of inter-message intervals)."""
        if len(self.recv_timestamps) < 3:
            return 0.0
        intervals = [
            (self.recv_timestamps[i + 1] - self.recv_timestamps[i]) * 1000.0
            for i in range(len(self.recv_timestamps) - 1)
        ]
        return round(statistics.stdev(intervals), 3)


# ------------------------------------------------------------ benchmark node
class CommBenchmarkNode(Node):
    """Single-run benchmark node."""

    def __init__(self):
        super().__init__("comm_benchmark")

        # -- parameters
        self.declare_parameter("mode", "native")        # "native" or "zmq"
        self.declare_parameter("robot_name", "BenchmarkBot")
        self.declare_parameter("duration", 30)           # seconds
        self.declare_parameter("warmup", 5)              # seconds to discard
        self.declare_parameter("output_dir",
                               os.path.expanduser("~/workspace/cloisim/benchmark_results"))

        self.mode: str        = self.get_parameter("mode").value
        self.robot_name: str  = self.get_parameter("robot_name").value
        self.duration: int    = self.get_parameter("duration").value
        self.warmup: int      = self.get_parameter("warmup").value
        self.output_dir: str  = self.get_parameter("output_dir").value

        # -- stats bookkeeping
        self.stats: dict[str, _TopicStats] = {}
        self._subs = []

        # -- create subscriptions
        prefix = f"/{self.robot_name}"
        for sensor_name, (suffix, msg_type, qos) in SENSOR_DEFS.items():
            topic = f"{prefix}/{suffix}"
            self.stats[sensor_name] = _TopicStats()
            sub = self.create_subscription(
                msg_type, topic,
                self._make_cb(sensor_name, msg_type),
                qos,
            )
            self._subs.append(sub)
            self.get_logger().info(f"  [{self.mode}] Subscribing: {topic}")

        # -- phase tracking
        self._phase = "waiting"   # waiting -> warmup -> measuring -> done
        self._phase_start: float = 0.0

        # -- timer for progress (1 Hz)
        self._progress_timer = self.create_timer(1.0, self._progress_cb)

        # -- CPU baseline
        psutil.cpu_percent(interval=None)

        self.get_logger().info(
            f"Benchmark node ready  mode={self.mode}  robot={self.robot_name}  "
            f"warmup={self.warmup}s  duration={self.duration}s"
        )

    # -------------------------------------------------------- subscription cb
    def _make_cb(self, name: str, msg_type):
        def _cb(msg):
            s = self.stats[name]
            s.count += 1
            s.recv_timestamps.append(time.monotonic())

            # latency from header stamp
            if hasattr(msg, "header") and msg.header.stamp.sec > 0:
                now_ns = self.get_clock().now().nanoseconds
                msg_ns = rclpy.time.Time.from_msg(msg.header.stamp).nanoseconds
                lat_ms = (now_ns - msg_ns) / 1e6
                if 0 < lat_ms < 10_000:
                    s.latencies_ms.append(lat_ms)

            # serialized size
            try:
                s.bytes_total += len(serialize_message(msg))
            except Exception:
                pass

            # first message triggers warmup
            if self._phase == "waiting":
                self._start_warmup()

        return _cb

    # ----------------------------------------------------------- phase logic
    def _start_warmup(self):
        self._phase = "warmup"
        self._phase_start = time.monotonic()
        self.get_logger().info(
            f"First message received — warming up for {self.warmup}s …"
        )

    def _start_measuring(self):
        self._phase = "measuring"
        self._phase_start = time.monotonic()
        # reset stats so warmup data is discarded
        for s in self.stats.values():
            s.reset()
        psutil.cpu_percent(interval=None)       # reset CPU counter
        self.get_logger().info(
            f"Warmup done — measuring for {self.duration}s …"
        )

    def _finish(self):
        self._phase = "done"
        elapsed = time.monotonic() - self._phase_start
        cpu = psutil.cpu_percent(interval=None)
        self._report(elapsed, cpu)
        raise SystemExit(0)

    # -------------------------------------------------------- progress timer
    def _progress_cb(self):
        now = time.monotonic()
        if self._phase == "warmup":
            if now - self._phase_start >= self.warmup:
                self._start_measuring()
            else:
                remaining = self.warmup - (now - self._phase_start)
                self.get_logger().info(f"  warmup … {remaining:.0f}s remaining")

        elif self._phase == "measuring":
            elapsed = now - self._phase_start
            if elapsed >= self.duration:
                self._finish()
            else:
                counts_str = "  ".join(
                    f"{n}:{s.count}" for n, s in self.stats.items()
                )
                self.get_logger().info(
                    f"  measuring {elapsed:.0f}/{self.duration}s  {counts_str}"
                )

    # --------------------------------------------------------------- report
    def _report(self, duration: float, cpu_pct: float):
        os.makedirs(self.output_dir, exist_ok=True)

        results: dict = {
            "meta": {
                "mode": self.mode,
                "robot_name": self.robot_name,
                "duration_sec": round(duration, 2),
                "warmup_sec": self.warmup,
                "cpu_usage_pct": round(cpu_pct, 1),
                "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S"),
            },
            "sensors": {},
        }

        header = (
            f"\n{'=' * 80}\n"
            f"  CLOiSim Comm Benchmark — mode={self.mode}  duration={duration:.1f}s\n"
            f"{'=' * 80}"
        )
        self.get_logger().info(header)
        self.get_logger().info(
            f"{'Sensor':<15} {'Hz':>8} {'BW MB/s':>10} {'Lat avg':>10} "
            f"{'Lat p95':>10} {'Jitter':>10} {'Msgs':>8}"
        )
        self.get_logger().info("-" * 80)

        for name, s in self.stats.items():
            hz = s.hz(duration)
            bw = s.bandwidth_mb_s(duration)
            lat = s.latency_stats()
            jitter = s.jitter_ms()

            self.get_logger().info(
                f"{name:<15} {hz:>8.2f} {bw:>10.3f} {lat['avg_ms']:>10.2f} "
                f"{lat['p95_ms']:>10.2f} {jitter:>10.2f} {s.count:>8d}"
            )

            results["sensors"][name] = {
                "hz": round(hz, 2),
                "bandwidth_mb_s": round(bw, 4),
                "latency": lat,
                "jitter_ms": jitter,
                "messages_received": s.count,
            }

        self.get_logger().info("-" * 80)
        self.get_logger().info(f"  CPU usage: {cpu_pct:.1f}%")
        self.get_logger().info("=" * 80)

        outfile = os.path.join(self.output_dir, f"benchmark_{self.mode}.json")
        with open(outfile, "w") as fp:
            json.dump(results, fp, indent=2)
        self.get_logger().info(f"Results saved → {outfile}")


# -------------------------------------------------------------------- main
def main(args=None):
    rclpy.init(args=args)
    node = CommBenchmarkNode()
    try:
        rclpy.spin(node)
    except (KeyboardInterrupt, SystemExit):
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()
