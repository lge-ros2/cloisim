using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace CLOiSim.Diagnostics
{
    /// <summary>
    /// Detects main-thread stalls (hard freezes) from a background thread, so a
    /// warning is logged even when the main thread is fully blocked (e.g. a
    /// synchronous GPU readback inside the URT sensor pipeline).
    ///
    /// Attach once to a persistent GameObject. Call FreezeWatchdog.Mark(...) right
    /// before/after heavy GPU work to leave a breadcrumb that pinpoints the stall.
    /// </summary>
    public sealed class FreezeWatchdog : MonoBehaviour
    {
        [Tooltip("Stall duration (ms) before a warning is logged.")]
        public int warnThresholdMs = 1000;

        [Tooltip("Escalate to error after this stall duration (ms). 0 disables.")]
        public int errorThresholdMs = 5000;

        [Tooltip("Watchdog polling interval (ms).")]
        public int pollIntervalMs = 200;

        [Tooltip("Also warn on per-frame hitches above this (ms) from the main thread.")]
        public int hitchThresholdMs = 250;

        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static long _lastHeartbeatMs;
        static volatile string _stage = "idle";
        // Suppress counter: when > 0 the watchdog skips stall detection.
        // Use Suppress()/Restore() around operations that are intentionally slow
        // (world loading, mesh import) to avoid false-positive stall warnings.
        static volatile int _suppressCount;

        // Immutable snapshot of diagnostic info, refreshed ONLY from the main thread
        // (see RefreshDiagSnapshot). DumpPreExitDiagnostics runs on the watchdog thread
        // and must never call SystemInfo/Time directly: once the GPU device is already
        // lost, those calls can trigger a native crash on a non-main thread (observed:
        // crash inside SystemInfo.graphicsDeviceType, called from this exact path).
        private sealed class DiagSnapshot
        {
            public string GpuName, GpuDriverVersion, CpuType;
            public string GpuType;
            public int VramMB, RamMB, CpuCores;
            public bool SupportsAsyncGpuReadback, SupportsGraphicsFence;
            public int FrameCount;
            public float RealtimeSinceStartup, TimeSinceLevelLoad;
        }

        static volatile DiagSnapshot _snapshot;

        /// <summary>Call from the main thread (e.g. LateUpdate) to refresh cached diagnostics.</summary>
        static void RefreshDiagSnapshot()
        {
            _snapshot = new DiagSnapshot
            {
                GpuName = SystemInfo.graphicsDeviceName,
                GpuType = SystemInfo.graphicsDeviceType.ToString(),
                GpuDriverVersion = SystemInfo.graphicsDeviceVersion,
                VramMB = SystemInfo.graphicsMemorySize,
                RamMB = SystemInfo.systemMemorySize,
                CpuType = SystemInfo.processorType,
                CpuCores = SystemInfo.processorCount,
                SupportsAsyncGpuReadback = SystemInfo.supportsAsyncGPUReadback,
                SupportsGraphicsFence = SystemInfo.supportsGraphicsFence,
                FrameCount = Time.frameCount,
                RealtimeSinceStartup = Time.realtimeSinceStartup,
                TimeSinceLevelLoad = Time.timeSinceLevelLoad,
            };
        }

        Thread _thread;
        volatile bool _running;

        /// <summary>Breadcrumb: set the current heavy stage so stall logs say where it hung.</summary>
        public static void Mark(string stage) => _stage = stage;

        /// <summary>
        /// Refresh the heartbeat without changing the stage — use from slow synchronous
        /// operations that run on the main thread (e.g. per-yield in loading coroutines)
        /// to prevent false-positive stall warnings.
        /// </summary>
        public static void Ping() =>
            Volatile.Write(ref _lastHeartbeatMs, Clock.ElapsedMilliseconds);

        /// <summary>
        /// Suppress stall warnings for the duration of an intentionally slow main-thread
        /// operation (world/mesh loading). Calls are reference-counted; every Suppress()
        /// must be paired with a Restore().
        /// </summary>
        public static void Suppress() => Interlocked.Increment(ref _suppressCount);

        /// <summary>Restore stall detection after a paired Suppress() call.</summary>
        public static void Restore()
        {
            if (Interlocked.Decrement(ref _suppressCount) < 0)
                Interlocked.Increment(ref _suppressCount); // clamp to 0
            // Refresh heartbeat so the first unsuppressed frame doesn't false-fire.
            Volatile.Write(ref _lastHeartbeatMs, Clock.ElapsedMilliseconds);
        }

        void OnEnable()
        {
            Volatile.Write(ref _lastHeartbeatMs, Clock.ElapsedMilliseconds);
            RefreshDiagSnapshot(); // seed the cache before the first LateUpdate runs
            _running = true;
            _thread = new Thread(Watch) { IsBackground = true, Name = "FreezeWatchdog" };
            _thread.Start();
        }

        void OnDisable()
        {
            _running = false;
            _thread?.Join(500);
            _thread = null;
        }

        // LateUpdate runs on the main thread after rendering submission: a fresh
        // heartbeat here means the main thread is alive and past the render loop.
        void LateUpdate()
        {
            long now = Clock.ElapsedMilliseconds;
            long prev = Volatile.Read(ref _lastHeartbeatMs);
            long frameMs = now - prev;

            if (hitchThresholdMs > 0 && frameMs >= hitchThresholdMs)
            {
                UnityEngine.Debug.LogWarning(
                    $"[FreezeWatchdog] frame hitch ~{frameMs}ms at stage='{_stage}'");
            }

            Volatile.Write(ref _lastHeartbeatMs, now);

            // Refresh the diagnostics snapshot here (main thread) so the watchdog
            // thread never has to touch SystemInfo/Time itself during a stall.
            RefreshDiagSnapshot();
        }

        static void DumpPreExitDiagnostics(long stalledMs)
        {
            // IMPORTANT: this runs on the watchdog (background) thread. Never call
            // SystemInfo/Time/UnityEngine APIs other than Debug.Log directly here -
            // if the GPU device is already lost, touching those from a non-main
            // thread has been observed to crash natively before this log can even
            // be written (see: SystemInfo.graphicsDeviceType in the stack trace of
            // the "Graphics device is null" crash). Use the cached snapshot instead.
            var snap = _snapshot;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[FreezeWatchdog] ===== PRE-EXIT DIAGNOSTICS (stalled={stalledMs}ms, elapsed={Clock.ElapsedMilliseconds}ms, stage='{_stage}') =====");

            if (snap == null)
            {
                sb.AppendLine("[FreezeWatchdog] no cached diagnostics snapshot available yet (stalled before first LateUpdate).");
            }
            else
            {
                sb.AppendLine($"[FreezeWatchdog] frame={snap.FrameCount}"
                    + $" realtime={snap.RealtimeSinceStartup:F1}s"
                    + $" timeSinceLevelLoad={snap.TimeSinceLevelLoad:F1}s"
                    + " (cached, last refreshed before the stall)");
                sb.AppendLine($"[FreezeWatchdog] GPU: {snap.GpuName}"
                    + $" ({snap.GpuType})"
                    + $" VRAM={snap.VramMB}MB"
                    + $" driverVersion={snap.GpuDriverVersion}");
                sb.AppendLine($"[FreezeWatchdog] sys: RAM={snap.RamMB}MB"
                    + $" CPU={snap.CpuType}"
                    + $" cores={snap.CpuCores}");
                sb.AppendLine($"[FreezeWatchdog] supportsAsyncGPUReadback={snap.SupportsAsyncGpuReadback}"
                    + $" supportsGraphicsFence={snap.SupportsGraphicsFence}");
            }

            UnityEngine.Debug.LogError(sb.ToString());

            URTSensorManager.DumpDiagHistory($"FreezeWatchdog pre-exit (stalled={stalledMs}ms)");
        }

        void Watch()
        {
            long lastReported = -1;
            while (_running)
            {
                Thread.Sleep(pollIntervalMs);

                long now = Clock.ElapsedMilliseconds;
                long stalledFor = now - Volatile.Read(ref _lastHeartbeatMs);
                if (stalledFor < warnThresholdMs || _suppressCount > 0)
                {
                    lastReported = -1;
                    continue;
                }

                // Report once per crossed threshold bucket to avoid log spam.
                long bucket = stalledFor / warnThresholdMs;
                if (bucket == lastReported) continue;
                lastReported = bucket;

                // UnityEngine.Debug logging is thread-safe and reaches Player.log.
                string msg = $"[FreezeWatchdog] MAIN THREAD STALLED ~{stalledFor}ms at stage='{_stage}' elapsed={now}ms";
                if (errorThresholdMs > 0 && stalledFor >= errorThresholdMs)
                {
                    UnityEngine.Debug.LogError(msg);
                    DumpPreExitDiagnostics(stalledFor);
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    System.Environment.Exit(1);
#endif
                }
                else
                    UnityEngine.Debug.LogWarning(msg);
            }
        }
    }
}