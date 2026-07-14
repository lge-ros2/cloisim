/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };
	public ModeType Mode = ModeType.NONE;

	// --- Idle-freeze diagnostics (all thread-safe, negligible overhead) ---
	/// <summary>AsyncGPUReadback requests currently in-flight across all sensors.</summary>
	public static long GpuReadbackInflight => Interlocked.Read(ref s_gpuReadbackInflight);
	private static long s_gpuReadbackInflight;

	/// <summary>DeviceThreadRx idle-yield iterations (accumulated; reset by dump).</summary>
	public static long RxThreadYields => Interlocked.Read(ref s_rxThreadYields);
	private static long s_rxThreadYields;
	public static long ResetRxThreadYields() => Interlocked.Exchange(ref s_rxThreadYields, 0);

	/// <summary>Call immediately before AsyncGPUReadback.Request().</summary>
	public static void GpuReadbackBegin() => Interlocked.Increment(ref s_gpuReadbackInflight);
	/// <summary>Call as the first statement inside the readback callback.</summary>
	public static void GpuReadbackEnd()   => Interlocked.Decrement(ref s_gpuReadbackInflight);

	// --- Worker-thread teardown gate (defends the GC stop-the-world abort) ---
	//
	// A TX worker thread serializing a protobuf message grows its MemoryStream
	// buffer, which allocates managed memory. That allocation can trigger the
	// Boehm GC, whose stop-the-world phase suspends every managed thread via
	// signals. If the main thread is concurrently being torn down (destroying
	// native UI/Mesh objects) the suspend can fail — "pthread_kill failed at
	// suspend" — and the runtime aborts (SIGABRT).
	//
	// Once shutdown begins we stop worker threads from generating/serializing
	// messages, so they no longer allocate and never enter GC during the
	// dangerous teardown window. The flag is volatile (read in worker loops,
	// written from the main thread) and is set on application quit.
	private static volatile bool s_shuttingDown = false;

	/// <summary>True once application teardown has begun; worker threads halt allocation.</summary>
	public static bool IsShuttingDown => s_shuttingDown;

	/// <summary>
	/// Signal that teardown has begun. Idempotent and safe to call from any
	/// teardown path; worker threads stop serializing/allocating immediately.
	/// </summary>
	public static void SignalShuttingDown() => s_shuttingDown = true;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void InitTeardownGate()
	{
		// Reset on play start (statics survive domain-reload-disabled play mode
		// and scene reloads) and subscribe exactly once to the quit event.
		s_shuttingDown = false;
		Application.quitting -= SignalShuttingDown;
		Application.quitting += SignalShuttingDown;
	}

	/// <summary>
	/// Drain in-flight AsyncGPUReadback requests before freeing GPU resources on
	/// teardown/quit. Skips the blocking AsyncGPUReadback.WaitAllRequests() call
	/// entirely when nothing is in flight — the common teardown case — so the main
	/// thread never blocks needlessly. When requests ARE pending we must still wait
	/// (WaitAllRequests pumps and completes their callbacks) to avoid the GfxDevice
	/// thread touching freed buffers (SIGSEGV).
	///
	/// Returns true when it is safe for the caller to release GPU resources
	/// immediately. Returns false when the drain was abandoned because the GPU
	/// did not quiesce within warnThresholdMs (wedged GPU / TDR / Xid) — callers
	/// MUST NOT free GPU resources immediately in that case; route them through
	/// URTSensorManager.DeferDispose() instead, since the readbacks are still
	/// in flight and an immediate free is a use-after-free (SIGSEGV).
	/// </summary>
	public static bool DrainReadbacksForTeardown(in int warnThresholdMs = 1000)
	{
		if (Interlocked.Read(ref s_gpuReadbackInflight) <= 0)
			return true;

		// Suppress FreezeWatchdog for the duration of this intentional blocking drain:
		// deleting a model can tear down several GPU-backed devices (Camera/DepthCamera/Lidar)
		// back to back in the same OnDestroy pass, each blocking the main thread for up to
		// warnThresholdMs. Left unsuppressed, the watchdog treats this expected teardown
		// stall as a real freeze and force-exits the process (Environment.Exit).
		CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
		try
		{
			var sw = Stopwatch.StartNew();

			// Probe the GPU with a bounded fence poll BEFORE the unbounded WaitAllRequests().
			// WaitAllRequests() cannot be interrupted, but we CAN decline to enter it: a wedged
			// GPU (TDR / GSP-death / Xid 109) never signals the fence, so we abandon the drain
			// instead of hanging the main thread forever. Pending readbacks are left in flight;
			// their buffers must be freed through the fence-gated deferred path, never immediately.
			if (SystemInfo.supportsGraphicsFence)
			{
				var probe = Graphics.CreateGraphicsFence(
					UnityEngine.Rendering.GraphicsFenceType.CPUSynchronisation,
					UnityEngine.Rendering.SynchronisationStageFlags.AllGPUOperations);
				var deadlineMs = warnThresholdMs > 0 ? warnThresholdMs : 1000;
				while (!probe.passed)
				{
					if (sw.ElapsedMilliseconds > deadlineMs)
					{
						Debug.LogWarning(
							$"[Device] GPU did not quiesce within {deadlineMs}ms during teardown — " +
							$"abandoning blocking readback drain (GPU may be lost). " +
							$"inflight now={Interlocked.Read(ref s_gpuReadbackInflight)}");
						return false;
					}
					Thread.Sleep(1);
				}
			}

			// GPU is responsive (or graphics fences unsupported): completing readbacks is now fast.
			UnityEngine.Rendering.AsyncGPUReadback.WaitAllRequests();
			sw.Stop();

			if (sw.ElapsedMilliseconds > warnThresholdMs)
			{
				Debug.LogWarning(
					$"[Device] AsyncGPUReadback.WaitAllRequests() took {sw.ElapsedMilliseconds}ms " +
					$"(> {warnThresholdMs}ms) during teardown — possible GPU/driver stall. " +
					$"inflight now={Interlocked.Read(ref s_gpuReadbackInflight)}");
			}

			return true;
		}
		finally
		{
			CLOiSim.Diagnostics.FreezeWatchdog.Restore();
		}
	}

	protected ConcurrentQueue<ProtoBuf.IExtensible> _messageQueue = new();

	[NonSerialized]
	private DeviceMessageQueue _deviceMessageQueue = new();
	private DevicePose _devicePose = new();

	private const double HighResolutionThresholdPeriod = 1.0 / 50.0; // ≥50 Hz sensors use high-res spin-wait loop instead of timer-based sleep

	// Pool DeviceMessage objects to avoid per-frame GC allocations
	private static readonly ConcurrentBag<DeviceMessage> _deviceMessagePool = new();
	private const int MaxPoolSize = 128;

	// Event-driven TX: signal from readback callbacks to wake the TX thread
	// immediately instead of waiting for the next timer-based poll.
	private readonly AutoResetEvent _txDataReady = new(false);

	[SerializeField]
	private string _deviceName = string.Empty;

	[SerializeField]
	private float _updateRate = -1;

	private bool _debuggingOn = true;

	[SerializeField]
	private bool _visualize = true;

	[SerializeField]
	private float _transportingTimeSeconds = 0;

	private Coroutine _coroutine = null;
	private Coroutine _visualizeCoroutine = null;
	private Thread _thread = null;

	// volatile: read in worker-thread loops, written from the main thread before
	// an unconditional Join(); guarantees the stop is observed (no hung join).
	private volatile bool _running = false;

	// Synthetic monotonic timestamp for fixed-dt publishing.
	// Advances by exactly UpdatePeriod per publish for jitter-free timestamps.
	private double _syntheticTime = -1;
	private readonly object _syntheticTimeLock = new();

	public Clock Clock { get; protected set; }

	public float UpdatePeriod => 1f / UpdateRate;

	public float UpdateRate => _updateRate;

	public string DeviceName
	{
		get => _deviceName;
		set => _deviceName = value;
	}

	public bool EnableDebugging
	{
		get => _debuggingOn;
		set => _debuggingOn = value;
	}

	public bool EnableVisualize
	{
		get => _visualize;
		set
		{
			if (_visualize == value)
				return;

			_visualize = value;
			_lastAppliedVisualize = _visualize;
			ApplyVisualize();
		}
	}

	private bool _lastAppliedVisualize = true;

	private void ApplyVisualize()
	{
		if (_visualize)
		{
			if (_visualizeCoroutine == null)
			{
				_visualizeCoroutine = StartCoroutine(OnVisualize());
			}
		}
		else
		{
			if (_visualizeCoroutine != null)
			{
				StopCoroutine(_visualizeCoroutine);
				_visualizeCoroutine = null;
				OnVisualizeStop();
			}
		}
	}

	private bool _visualizeApplyPending = false;

	// Inspector's default field drawer writes directly to the [SerializeField]
	// backing field via SerializedObject, bypassing the EnableVisualize setter
	// above. OnValidate is Unity's hook for detecting that kind of direct edit
	// (including in Play mode). Unity forbids SendMessage-triggering calls
	// (AddComponent/SetParent/set_layer, all used transitively by ApplyVisualize's
	// coroutines) while still inside OnValidate's call stack, so just flag it and
	// defer the actual apply to the next Update().
	private void OnValidate()
	{
		if (_visualize != _lastAppliedVisualize)
		{
			_lastAppliedVisualize = _visualize;
			_visualizeApplyPending = true;
		}
	}

	private void Update()
	{
		if (_visualizeApplyPending)
		{
			_visualizeApplyPending = false;
			if (gameObject.activeInHierarchy && Application.isPlaying)
			{
				ApplyVisualize();
			}
		}
	}

	/// <summary>
	/// Return a DeviceMessage to the pool for reuse.
	/// Call after the Sender thread has finished publishing.
	/// </summary>
	public static void ReturnDeviceMessage(DeviceMessage msg)
	{
		if (msg != null && _deviceMessagePool.Count < MaxPoolSize)
		{
			_deviceMessagePool.Add(msg);
		}
	}

#if UNITY_EDITOR
	#region DIAGNOSTICS
	// Per-sensor publish Hz diagnostics
	private readonly Stopwatch _diagPublishSw = new();
	private int _diagPublishCount;
	private float _diagPublishHz;
	private const float DEVICE_DIAG_INTERVAL_SEC = 30f;

	/// <summary>Actual measured publish Hz (updated every DEVICE_DIAG_INTERVAL_SEC).</summary>
	public float PublishHz => _diagPublishHz;
	#endregion

	#region PROFILER
	private int _profFrameCount = 0;
	private double _profByteCount = 0;
	private float _periodForProfiler = 60f; // seconds

	private Stopwatch _profWatch = Stopwatch.StartNew();

	[ContextMenu("Reset Profiler")]
	private void ResetProfiler()
	{
		_profFrameCount = 0;
		_profByteCount = 0;
		_profWatch.Restart();
	}

	protected void UpdateProfiler(in string targetName, in double byteCount)
	{
		const double oneMegabyte = 1024.0 * 1024.0;

		_profFrameCount++;
		_profByteCount += byteCount;
		var seconds = _profWatch.Elapsed.TotalSeconds;
		if (seconds >= _periodForProfiler)
		{
			var hz = _profFrameCount / seconds;
			var mbPerSec = _profByteCount / seconds / oneMegabyte;
			var mbps = _profByteCount * 8.0 / seconds / oneMegabyte;
			Debug.Log($"[PROF][{targetName}] {DeviceName} Hz: {hz:F2} | Bandwidth: {mbps:F2} Mbps ({mbPerSec:F2} MB/s)");
			ResetProfiler();
		}
	}
	#endregion
#endif

	void Awake()
	{
		OnAwake();
		InitializeMessages();
	}

	void Start()
	{
		SetupMessages();
		StartCoroutine(DelayedStart());
	}

	private IEnumerator DelayedStart()
	{
		yield return new WaitForEndOfFrame();

		Clock = DeviceHelper.GetGlobalClock();

		OnStart();

		_running = true;

		switch (Mode)
		{
			case ModeType.TX:
				_coroutine = StartCoroutine(DeviceCoroutineTx());
				break;

			case ModeType.RX:
				_coroutine = StartCoroutine(DeviceCoroutineRx());
				break;

			case ModeType.TX_THREAD:
				_thread = new Thread(DeviceThreadTx);
				break;

			case ModeType.RX_THREAD:
				_thread = new Thread(DeviceThreadRx);
				break;

			case ModeType.NONE:
			default:
				_running = false;
				// Debug.LogWarning("Device(" + name + ") Mode is None");
				break;
		}

		if (_thread != null)
		{
			_thread.Start();
		}

		if (EnableVisualize)
		{
			_visualizeCoroutine = StartCoroutine(OnVisualize());
		}
	}

	/// <summary>
	/// Request the worker thread / TX-RX coroutine to stop and wake it so it
	/// exits promptly. Safe to call repeatedly and before OnDestroy (which still
	/// performs the join). Called when a model is being torn down so its sensor
	/// workers stop reading transforms/components before the native deactivation
	/// and destroy cascade runs on the main thread (avoids a background-thread
	/// race → native SIGSEGV).
	/// </summary>
	public void RequestStop()
	{
		_running = false;
		_txDataReady.Set();
	}

	protected void OnDestroy()
	{
		_running = false;

		if (_visualizeCoroutine != null)
		{
			StopCoroutine(_visualizeCoroutine);
			_visualizeCoroutine = null;
		}

		// Wake TX thread so it can exit cleanly
		_txDataReady.Set();

		_messageQueue.Clear();

		switch (Mode)
		{
			case ModeType.TX:
			case ModeType.RX:
				StopCoroutine(_coroutine);
				break;

			case ModeType.TX_THREAD:
			case ModeType.RX_THREAD:
				if (_thread != null && _thread.IsAlive)
				{
					_thread.Join();
				}
				break;

			case ModeType.NONE:
			default:
				break;
		}
#if UNITY_EDITOR
		Debug.Log($"Stop Device [{Mode.ToString()}] [{name}/{DeviceName}]");
#endif
		_deviceMessageQueue.Flush();
		_deviceMessageQueue.Dispose();
	}

	protected abstract void OnAwake();

	protected virtual void OnStart() { }

	protected virtual void OnReset() { }

	protected virtual IEnumerator OnVisualize()
	{
		yield return null;
	}

	// StopCoroutine() aborts OnVisualize() mid-execution, skipping any cleanup
	// it would otherwise reach (e.g. destroying a visualizer GameObject it
	// created). Devices that allocate visualization resources in OnVisualize()
	// should tear them down here instead.
	protected virtual void OnVisualizeStop() { }

	/// <summary>
	/// Initialize message objects only
	/// </summary>
	protected virtual void InitializeMessages() { }

	/// <summary>
	/// Setup message object after initialized
	/// </summary>
	protected virtual void SetupMessages() { }

	// Used for RX
	protected virtual void ProcessReceivedDeviceMessage(DeviceMessage receivedMessage) { }

	// Used for TX
	protected virtual void GenerateMessage()
	{
		if (_messageQueue.IsEmpty)
			return;

		// Flush all queued messages immediately -- no sleeping between them.
		// Data has already been rate-limited at the render/capture stage.
		while (_messageQueue.TryDequeue(out var msg))
		{
			try
			{
				PushDeviceMessage(msg);
				OnMessagePublished(msg);
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"failed to PushDeviceMessage(): {ex.Message}");
			}
		}
	}

	protected virtual void OnMessagePublished(ProtoBuf.IExtensible message) { }

	/// <summary>
	/// Enqueue a protobuf message and wake the TX thread immediately
	/// so data is published with minimal latency.
	/// </summary>
	protected void EnqueueMessage(ProtoBuf.IExtensible message)
	{
		_messageQueue.Enqueue(message);
		_txDataReady.Set();
	}

	private IEnumerator DeviceCoroutineTx()
	{
		var waitForSeconds = new WaitForSeconds(WaitPeriod());
		while (_running)
		{
			if (!s_shuttingDown)
			{
				GenerateMessage();
			}
			yield return waitForSeconds;
		}
	}

	private IEnumerator DeviceCoroutineRx()
	{
		var waitUntil = new WaitUntil(() => _deviceMessageQueue.Count > 0);
		while (_running)
		{
			yield return waitUntil;
			if (PopDeviceMessage(out var receivedMessage) && receivedMessage != null)
			{
				ProcessReceivedDeviceMessage(receivedMessage);
			}
		}
	}

	private void DeviceThreadTx()
	{
		// Two TX patterns share this thread:
		//
		// 1. Event-driven (cameras, lidar, contact, micom):
		//    Data arrives via EnqueueMessage() which enqueues and signals the TX thread.
		//    Rate is controlled by the producer (e.g. CameraWorker coroutine).
		//    TX thread just waits for the signal, flushes, and loops.
		//
		// 2. Timer-polled (GPS, IMU, JointState, Clock):
		//    GenerateMessage() override builds & pushes data when called.
		//    TX thread must call it at precise intervals.
		//
		// For ≥50 Hz sensors (period ≤ 20ms), OS timer granularity (~1-4ms)
		// is too coarse, so we use a Stopwatch-based spin-yield loop.
		// For <50 Hz sensors, WaitOne(timeout) provides sufficient accuracy.
		//
		// NOTE: UpdateRate may be set AFTER the thread starts (e.g., from SDF config),
		// so timing parameters are re-evaluated dynamically inside the loop.
#if UNITY_EDITOR
		_diagPublishSw.Start();
#endif
		// Timing parameters — recomputed when UpdateRate changes
		float lastUpdateRate = -1;
		long periodTicks = 0;
		bool useHighRes = false;
		long nextDeadline = 0;

		while (_running)
		{
			// Stop allocating once teardown begins (see s_shuttingDown).
			if (s_shuttingDown)
				break;

			if (UpdateRate <= 0)
			{
				Thread.Sleep(100);
				continue;
			}

			// Re-evaluate timing when UpdateRate changes
			if (UpdateRate != lastUpdateRate)
			{
				lastUpdateRate = UpdateRate;
				periodTicks = (long)(UpdatePeriod * Stopwatch.Frequency);
				// Use high-res spin loop for ≥50 Hz sensors (period ≤ 20ms).
				// OS timer-based WaitOne has ~1-4ms jitter on Linux, which is
				// unacceptable for 100Hz+ sensors and causes ~10% rate loss.
				useHighRes = periodTicks > 0 && UpdatePeriod <= HighResolutionThresholdPeriod;


				nextDeadline = Stopwatch.GetTimestamp() + periodTicks;
			}

			if (useHighRes)
			{
				// High-res path for ≥50 Hz timer-polled sensors (IMU, JointState, etc.)
				// Precise Stopwatch-based spin-yield until next deadline.
				GenerateMessage();

				while (true)
				{
					var remaining = nextDeadline - Stopwatch.GetTimestamp();
					if (remaining <= 0)
						break;

					if (remaining > Stopwatch.Frequency / 500) // > 2ms
						Thread.Sleep(1);
					else if (remaining > Stopwatch.Frequency / 2000) // > 0.5ms
						Thread.Yield();
					else
						Thread.SpinWait(1);
				}

				nextDeadline += periodTicks;
				var now = Stopwatch.GetTimestamp();
				if (nextDeadline < now - 2 * periodTicks)
					nextDeadline = now + periodTicks;
			}
			else
			{
				// Low-rate path for <50 Hz sensors.
				// Event-driven sensors (cameras, lidar): SignalDataReady() wakes
				// WaitOne immediately, flush and loop — no spin-wait needed.
				// Timer-polled sensors (GPS): WaitOne times out at UpdatePeriod.
				var timeoutMs = Mathf.Max(1, Mathf.RoundToInt(UpdatePeriod * 1000f));
				_txDataReady.WaitOne(timeoutMs);
				GenerateMessage();
			}

#if UNITY_EDITOR
			// Periodic per-sensor Hz diagnostics
			var elapsed = (float)_diagPublishSw.Elapsed.TotalSeconds;
			if (elapsed >= DEVICE_DIAG_INTERVAL_SEC && _diagPublishCount > 0)
			{
				_diagPublishHz = _diagPublishCount / elapsed;
				Debug.Log($"[Device:{_deviceName}] publishHz={_diagPublishHz:F1} (target={UpdateRate:F0}) msgs={_diagPublishCount}/{elapsed:F1}s");
				_diagPublishCount = 0;
				_diagPublishSw.Restart();
			}
#endif
		}
	}

	private void DeviceThreadRx()
	{
		while (_running)
		{
			if (_deviceMessageQueue.Count > 0)
			{
				if (PopDeviceMessage(out var receivedMessage) && receivedMessage != null)
				{
					ProcessReceivedDeviceMessage(receivedMessage);
				}
			}
			else
			{
				Interlocked.Increment(ref s_rxThreadYields);
				// Sleep(1) instead of Thread.Yield(): on Linux when no other
				// thread is ready, Yield returns immediately and the RX thread
				// burns a full core. Many sensors collectively starve render
				// and physics threads (observed ~5.9M yields/sec). 1ms wakeup
				// granularity is well below sensor RX needs.
				Thread.Sleep(1);
			}
		}
	}

	public bool PushDeviceMessage<T>(T instance) where T : ProtoBuf.IExtensible
	{
		// Do not serialize (and thus allocate) once teardown has begun: a GC
		// triggered here while the main thread destroys native objects can
		// abort the process via a failed stop-the-world suspend.
		if (s_shuttingDown)
			return false;

		try
		{
			if (!_deviceMessagePool.TryTake(out var deviceMessage))
			{
				deviceMessage = new DeviceMessage();
			}

			// Raw binary fast-path for image types — bypasses protobuf serialization
			if (instance is cloisim.msgs.Image img)
			{
				deviceMessage.SetRawImage(img);
			}
			else if (instance is cloisim.msgs.Segmentation seg)
			{
				deviceMessage.SetRawSegmentation(seg);
			}
			else if (instance is cloisim.msgs.Images imgs)
			{
				deviceMessage.SetRawImages(imgs);
			}
			else
			{
				// Protobuf fallback for non-image sensors (lidar, IMU, etc.)
				deviceMessage.SetMessage(instance);
			}

			var pushed = _deviceMessageQueue.Push(deviceMessage);
			if (pushed)
			{
#if UNITY_EDITOR
				Interlocked.Increment(ref _diagPublishCount);
#endif
			}
			else
			{
				ReturnDeviceMessage(deviceMessage);
			}
			return pushed;
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"ERROR: PushDeviceMessage<{typeof(T).ToString()}>(): {ex.Message}");
			return false;
		}
	}

	public bool PushDeviceMessage(in byte[] data)
	{
		if (s_shuttingDown)
			return false;

		try
		{
			if (!_deviceMessagePool.TryTake(out var deviceMessage))
			{
				deviceMessage = new DeviceMessage();
			}

			if (deviceMessage.SetMessage(data))
			{
				var pushed = _deviceMessageQueue.Push(deviceMessage);
				if (!pushed)
				{
					ReturnDeviceMessage(deviceMessage);
				}
				return pushed;
			}
			else
			{
				ReturnDeviceMessage(deviceMessage);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("ERROR: PushDeviceMessage(): " + ex.Message);
		}

		return false;
	}

	public bool PopDeviceMessage(out DeviceMessage data)
	{
		return _deviceMessageQueue.Pop(out data);
	}

	public void Reset()
	{
		// Flush queues BEFORE resetting synthetic time to avoid a race
		// where the TX thread re-snaps _syntheticTime between the reset
		// and the flush, wasting the snap and causing the first post-reset
		// timestamp to increment from a stale value instead of re-snapping.
		_messageQueue.Clear();
		_deviceMessageQueue.Flush();

		lock (_syntheticTimeLock)
		{
			_syntheticTime = -1;
		}

		OnReset();
	}

	protected float WaitPeriod(in float messageGenerationTimeInSec = 0)
	{
		var waitTime = UpdatePeriod - messageGenerationTimeInSec - _transportingTimeSeconds;
		// Debug.LogFormat(_deviceName + ": waitTime({0}) = period({1}) - elapsedTime({2}) - TransportingTime({3})",
		// 	waitTime.ToString("F5"), UpdatePeriod.ToString("F5"), messageGenerationTimeInSec.ToString("F5"), _transportingTimeSeconds.ToString("F5"));
		return (waitTime < 0) ? 0 : waitTime;
	}

	protected int WaitPeriodInMilliseconds()
	{
		return Mathf.CeilToInt(WaitPeriod() * 1000f);
	}

	public void SetUpdateRate(in float value)
	{
		_updateRate = value;
	}

	public void SetTransportedTime(in float value)
	{
		_transportingTimeSeconds = value;
	}

	public Pose GetPose()
	{
		return _devicePose.Get();
	}

	public void UpdatePose()
	{
		_devicePose.Store(transform);
	}

	/// <summary>
	/// Get the next synthetic publish timestamp with fixed delta.
	/// First call: snaps to current SimTime.
	/// Subsequent calls: advances by exactly 1.0/UpdateRate (double precision)
	/// for jitter-free timestamps. Thread-safe.
	/// </summary>
	protected double GetNextSyntheticTime()
	{
		lock (_syntheticTimeLock)
		{
			if (_syntheticTime < 0)
			{
				_syntheticTime = (Clock != null) ? Clock.SimTime : Time.timeAsDouble;
#if UNITY_EDITOR
				Debug.Log($"[Device:{_deviceName}] Snap synthetic time to SimTime: {_syntheticTime:F6}");
#endif
			}
			else
			{
				// Use double division for exact period (avoids float truncation)
				_syntheticTime += 1.0 / (double)UpdateRate;
			}
			return _syntheticTime;
		}
	}
}
