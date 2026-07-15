using System;
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
    // Must run its LateUpdate before SensorRenderManager (default order 0): both resume
    // in the same frame right after a big main-thread stall, and RecentlyHadBigHitch()
    // needs _lastHitchMs already written for that exact frame, not one frame later.
    [DefaultExecutionOrder(-500)]
    public sealed class FreezeWatchdog : MonoBehaviour
    {
        [Tooltip("Stall duration (ms) before a warning is logged.")]
        public int warnThresholdMs = 1000;

        [Tooltip("Escalate to error after this stall duration (ms). 0 disables.")]
        public int errorThresholdMs = 5000;

        [Tooltip("Watchdog polling interval (ms).")]
        public int pollIntervalMs = 200;

        [Tooltip("Also warn on per-frame hitches above this (ms) from the main thread.")]
        public int hitchThresholdMs = 500;

        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static long _lastHeartbeatMs;
        static long _lastSnapshotMs;
        static long _lastHitchMs = long.MinValue;

        /// <summary>Minimum interval (ms) between diagnostic snapshot refreshes.</summary>
        const long SnapshotRefreshIntervalMs = 500;
        static volatile string _stage = "idle";
        // Suppress counter: when > 0 the watchdog skips stall detection.
        // Use Suppress()/Restore() around operations that are intentionally slow
        // (world loading, mesh import) to avoid false-positive stall warnings.
        static volatile int _suppressCount;

#if UNITY_EDITOR
        // Cached on the main thread via the pauseStateChanged callback: reading
        // EditorApplication.isPaused directly from the watchdog thread is a native
        // call and unsafe off the main thread, same reasoning as the SystemInfo
        // calls guarded elsewhere in this file.
        static volatile bool _editorPaused;
#endif

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

        /// <summary>
        /// Whether a frame hitch above <see cref="FreezeWatchdog.hitchThresholdMs"/> was
        /// recorded within the last <paramref name="windowMs"/> milliseconds. A huge hitch
        /// usually means a heavy synchronous import (mesh/SDF) just ran on the main thread;
        /// GPU resource uploads it triggered may still be settling, so callers that submit
        /// their own render/GPU work (e.g. SensorRenderManager) can use this to skip a beat
        /// instead of racing that settling window.
        /// </summary>
        public static bool RecentlyHadBigHitch(long windowMs = 250) =>
            Clock.ElapsedMilliseconds - Volatile.Read(ref _lastHitchMs) < windowMs;

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
#if UNITY_EDITOR
            _editorPaused = UnityEditor.EditorApplication.isPaused;
            UnityEditor.EditorApplication.pauseStateChanged += OnEditorPauseStateChanged;
#endif
            _running = true;
            _thread = new Thread(Watch) { IsBackground = true, Name = "FreezeWatchdog" };
            _thread.Start();
        }

        void OnDisable()
        {
            _running = false;
            _thread?.Join(500);
            _thread = null;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPauseStateChanged;
#endif
        }

#if UNITY_EDITOR
        static void OnEditorPauseStateChanged(UnityEditor.PauseState state)
        {
            _editorPaused = state == UnityEditor.PauseState.Paused;
            // Paused frames don't call LateUpdate, so resuming would otherwise look
            // like one giant stall. Refresh the heartbeat on both edges.
            Volatile.Write(ref _lastHeartbeatMs, Clock.ElapsedMilliseconds);
        }
#endif

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
                Volatile.Write(ref _lastHitchMs, now);
            }

            Volatile.Write(ref _lastHeartbeatMs, now);

            // Refresh the diagnostics snapshot here (main thread) so the watchdog
            // thread never has to touch SystemInfo/Time itself during a stall.
            // Throttled: this allocates a new snapshot object + strings, and the
            // dump is only ever read after a multi-second stall, so per-frame
            // freshness buys nothing but adds constant GC pressure every frame.
            if (now - _lastSnapshotMs >= SnapshotRefreshIntervalMs)
            {
                _lastSnapshotMs = now;
                RefreshDiagSnapshot();
            }
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
#if UNITY_EDITOR
                if (stalledFor < warnThresholdMs || _suppressCount > 0 || _editorPaused)
#else
                if (stalledFor < warnThresholdMs || _suppressCount > 0)
#endif
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
                    // Diagnostics are best-effort: a bug in the dump path (e.g. touching a
                    // main-thread-only API) must never prevent the actual termination below.
                    try
                    {
                        DumpPreExitDiagnostics(stalledFor);
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogError($"[FreezeWatchdog] DumpPreExitDiagnostics failed: {e}");
                    }
#if UNITY_EDITOR
                    // EditorApplication.isPlaying's setter calls into native code and requires
                    // the main thread; calling it directly from this background thread throws.
                    // delayCall is a plain delegate field (no native call on add), so queuing
                    // here is safe and the assignment itself runs on the main thread later.
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.isPlaying = false;
#else
                    // Environment.Exit() runs the managed shutdown sequence (ProcessExit
                    // handlers, native engine/graphics teardown) on THIS thread. If the main
                    // thread is wedged inside a native GPU driver call (the usual cause of a
                    // stall this long — see the "missing UAV"/Xid GPU fault path in
                    // URTSensorManager), that teardown can block on the same driver-level lock
                    // the main thread holds, so Exit() itself hangs and the process never dies.
                    // Kill() terminates the OS process directly (TerminateProcess/SIGKILL)
                    // without requiring any other thread's cooperation.
                    Process.GetCurrentProcess().Kill();
#endif
                }
                else
                    UnityEngine.Debug.LogWarning(msg);
            }
        }
    }
}