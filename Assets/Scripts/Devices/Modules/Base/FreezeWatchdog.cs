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

        Thread _thread;
        volatile bool _running;

        /// <summary>Breadcrumb: set the current heavy stage so stall logs say where it hung.</summary>
        public static void Mark(string stage) => _stage = stage;

        void OnEnable()
        {
            Volatile.Write(ref _lastHeartbeatMs, Clock.ElapsedMilliseconds);
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
        }

        void Watch()
        {
            long lastReported = -1;
            while (_running)
            {
                Thread.Sleep(pollIntervalMs);

                long now = Clock.ElapsedMilliseconds;
                long stalledFor = now - Volatile.Read(ref _lastHeartbeatMs);
                if (stalledFor < warnThresholdMs)
                {
                    lastReported = -1;
                    continue;
                }

                // Report once per crossed threshold bucket to avoid log spam.
                long bucket = stalledFor / warnThresholdMs;
                if (bucket == lastReported) continue;
                lastReported = bucket;

                // UnityEngine.Debug logging is thread-safe and reaches Player.log.
                string msg = $"[FreezeWatchdog] MAIN THREAD STALLED ~{stalledFor}ms at stage='{_stage}'";
                if (errorThresholdMs > 0 && stalledFor >= errorThresholdMs)
                    UnityEngine.Debug.LogError(msg);
                else
                    UnityEngine.Debug.LogWarning(msg);
            }
        }
    }
}