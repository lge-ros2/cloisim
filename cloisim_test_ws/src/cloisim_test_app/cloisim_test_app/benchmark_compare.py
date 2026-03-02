#!/usr/bin/env python3
"""
Comparison tool — reads benchmark results from both comm modes and produces
a side-by-side report with improvement/regression percentages.

Usage:
  ros2 run cloisim_test_app benchmark_compare
  # or directly:
  python3 benchmark_compare.py [--results-dir ~/workspace/cloisim/benchmark_results]
"""

import argparse
import json
import os
import sys


DEFAULT_DIR = os.path.expanduser("~/workspace/cloisim/benchmark_results")


def _load(path: str) -> dict:
    with open(path) as f:
        return json.load(f)


def _pct(val_new: float, val_old: float) -> str:
    """Return '(+12.3%)' or '(-5.1%)' relative change string."""
    if val_old == 0:
        return "(N/A)" if val_new == 0 else "(+inf)"
    change = ((val_new - val_old) / abs(val_old)) * 100.0
    sign = "+" if change >= 0 else ""
    return f"({sign}{change:.1f}%)"


def _delta_arrow(val_new: float, val_old: float, lower_is_better: bool = False) -> str:
    """Return a directional indicator: ▲ better, ▼ worse, ≈ same."""
    if val_old == 0 and val_new == 0:
        return "≈"
    if val_old == 0:
        return "▲" if not lower_is_better else "▼"
    ratio = val_new / val_old
    if 0.98 < ratio < 1.02:
        return "≈"
    if lower_is_better:
        return "▲" if ratio < 1 else "▼"
    return "▲" if ratio > 1 else "▼"


def compare(results_dir: str):
    zmq_path = os.path.join(results_dir, "benchmark_zmq.json")
    native_path = os.path.join(results_dir, "benchmark_native.json")

    missing = []
    if not os.path.isfile(zmq_path):
        missing.append(zmq_path)
    if not os.path.isfile(native_path):
        missing.append(native_path)
    if missing:
        print("ERROR: Missing result file(s):")
        for p in missing:
            print(f"  {p}")
        print("\nRun the benchmark for each mode first:")
        print("  ros2 run cloisim_test_app comm_benchmark --ros-args -p mode:=zmq")
        print("  ros2 run cloisim_test_app comm_benchmark --ros-args -p mode:=native")
        sys.exit(1)

    zmq = _load(zmq_path)
    native = _load(native_path)

    all_sensors = sorted(
        set(zmq.get("sensors", {}).keys()) | set(native.get("sensors", {}).keys())
    )

    # ---- header ----
    w = 100
    print("\n" + "=" * w)
    print("  CLOiSim Communication Benchmark — ZMQ Legacy  vs  ROS Native")
    print("=" * w)

    zmeta = zmq.get("meta", {})
    nmeta = native.get("meta", {})
    print(f"  ZMQ run  : {zmeta.get('timestamp','?')}  duration={zmeta.get('duration_sec',0)}s  CPU={zmeta.get('cpu_usage_pct',0):.1f}%")
    print(f"  Native   : {nmeta.get('timestamp','?')}  duration={nmeta.get('duration_sec',0)}s  CPU={nmeta.get('cpu_usage_pct',0):.1f}%")
    print("-" * w)

    # ---- per-sensor comparison ----
    fmt_hdr = (
        f"{'Sensor':<15}"
        f" | {'Hz (ZMQ)':>10} {'Hz (NAT)':>10} {'Δ':>10}"
        f" | {'Lat ms(ZMQ)':>12} {'Lat ms(NAT)':>12} {'Δ':>10}"
        f" | {'Jitter(ZMQ)':>12} {'Jitter(NAT)':>12}"
    )
    print(fmt_hdr)
    print("-" * w)

    for sensor in all_sensors:
        sz = zmq.get("sensors", {}).get(sensor, {})
        sn = native.get("sensors", {}).get(sensor, {})

        hz_z = sz.get("hz", 0)
        hz_n = sn.get("hz", 0)

        lat_z = sz.get("latency", {}).get("avg_ms", 0)
        lat_n = sn.get("latency", {}).get("avg_ms", 0)

        jit_z = sz.get("jitter_ms", 0)
        jit_n = sn.get("jitter_ms", 0)

        hz_delta = f"{_delta_arrow(hz_n, hz_z)} {_pct(hz_n, hz_z)}"
        lat_delta = f"{_delta_arrow(lat_n, lat_z, lower_is_better=True)} {_pct(lat_n, lat_z)}"

        print(
            f"{sensor:<15}"
            f" | {hz_z:>10.2f} {hz_n:>10.2f} {hz_delta:>10}"
            f" | {lat_z:>12.2f} {lat_n:>12.2f} {lat_delta:>10}"
            f" | {jit_z:>12.2f} {jit_n:>12.2f}"
        )

    print("-" * w)

    # ---- bandwidth comparison ----
    print(f"\n{'Sensor':<15} | {'BW MB/s (ZMQ)':>15} {'BW MB/s (NAT)':>15} {'Δ':>12}")
    print("-" * 65)
    for sensor in all_sensors:
        sz = zmq.get("sensors", {}).get(sensor, {})
        sn = native.get("sensors", {}).get(sensor, {})
        bw_z = sz.get("bandwidth_mb_s", 0)
        bw_n = sn.get("bandwidth_mb_s", 0)
        print(
            f"{sensor:<15} | {bw_z:>15.4f} {bw_n:>15.4f} {_pct(bw_n, bw_z):>12}"
        )
    print("-" * 65)

    # ---- latency percentiles ----
    print(f"\n{'Sensor':<15} | {'Mode':<8} {'avg':>8} {'min':>8} {'p50':>8} {'p95':>8} {'p99':>8} {'max':>8} {'stddev':>8}")
    print("-" * 90)
    for sensor in all_sensors:
        for mode_label, data in [("ZMQ", zmq), ("Native", native)]:
            sd = data.get("sensors", {}).get(sensor, {}).get("latency", {})
            print(
                f"{sensor:<15} | {mode_label:<8}"
                f" {sd.get('avg_ms',0):>8.2f}"
                f" {sd.get('min_ms',0):>8.2f}"
                f" {sd.get('median_ms',0):>8.2f}"
                f" {sd.get('p95_ms',0):>8.2f}"
                f" {sd.get('p99_ms',0):>8.2f}"
                f" {sd.get('max_ms',0):>8.2f}"
                f" {sd.get('stddev_ms',0):>8.2f}"
            )
        print()
    print("=" * w)

    # ---- summary ----
    total_hz_z = sum(zmq.get("sensors", {}).get(s, {}).get("hz", 0) for s in all_sensors)
    total_hz_n = sum(native.get("sensors", {}).get(s, {}).get("hz", 0) for s in all_sensors)
    total_bw_z = sum(zmq.get("sensors", {}).get(s, {}).get("bandwidth_mb_s", 0) for s in all_sensors)
    total_bw_n = sum(native.get("sensors", {}).get(s, {}).get("bandwidth_mb_s", 0) for s in all_sensors)

    print("\nSummary:")
    print(f"  Total throughput    : ZMQ {total_hz_z:.1f} Hz  vs  Native {total_hz_n:.1f} Hz  {_pct(total_hz_n, total_hz_z)}")
    print(f"  Total bandwidth    : ZMQ {total_bw_z:.3f} MB/s  vs  Native {total_bw_n:.3f} MB/s  {_pct(total_bw_n, total_bw_z)}")
    print(f"  CPU usage          : ZMQ {zmeta.get('cpu_usage_pct',0):.1f}%  vs  Native {nmeta.get('cpu_usage_pct',0):.1f}%")
    print()

    # ---- save combined report ----
    combined = {
        "zmq": zmq,
        "native": native,
        "summary": {
            "total_hz_zmq": round(total_hz_z, 2),
            "total_hz_native": round(total_hz_n, 2),
            "total_bw_zmq_mb_s": round(total_bw_z, 4),
            "total_bw_native_mb_s": round(total_bw_n, 4),
            "cpu_zmq_pct": zmeta.get("cpu_usage_pct", 0),
            "cpu_native_pct": nmeta.get("cpu_usage_pct", 0),
        },
    }
    report_path = os.path.join(results_dir, "benchmark_comparison.json")
    with open(report_path, "w") as f:
        json.dump(combined, f, indent=2)
    print(f"Combined report saved → {report_path}")


def main(args=None):
    parser = argparse.ArgumentParser(description="Compare CLOiSim comm benchmarks")
    parser.add_argument(
        "--results-dir", default=DEFAULT_DIR,
        help="Directory containing benchmark_zmq.json and benchmark_native.json",
    )
    parsed, _ = parser.parse_known_args(args=sys.argv[1:])
    compare(parsed.results_dir)


if __name__ == "__main__":
    main()
