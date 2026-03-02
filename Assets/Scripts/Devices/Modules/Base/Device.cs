/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using Debug = UnityEngine.Debug;

public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };
	public ModeType Mode = ModeType.NONE;

	protected ConcurrentQueue<global::ProtoBuf.IExtensible> _messageQueue = new();

	[NonSerialized]
	private DeviceMessageQueue _deviceMessageQueue = new();
	private DevicePose _devicePose = new();

	// Pool DeviceMessage objects to avoid per-frame GC allocations
	private static readonly ConcurrentBag<DeviceMessage> _deviceMessagePool = new();

	// Event-driven TX: signal from readback callbacks to wake the TX thread
	// immediately instead of waiting for the next timer-based poll.
	private readonly AutoResetEvent _txDataReady = new(false);

	// Per-sensor publish Hz diagnostics
	private readonly Stopwatch _diagPublishSw = new();
	private int _diagPublishCount;
	private float _diagPublishHz;
	private const float DEVICE_DIAG_INTERVAL_SEC = 10f;

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
	private Thread _thread = null;

	private bool _running = false;

	// Synthetic monotonic timestamp for fixed-dt publishing.
	// Advances by exactly UpdatePeriod per publish for jitter-free timestamps.
	private double _syntheticTime = -1;
	private readonly object _syntheticTimeLock = new();

	public float UpdatePeriod => 1f / UpdateRate;

	public float UpdateRate => _updateRate;

	/// <summary>Actual measured publish Hz (updated every 10s).</summary>
	public float PublishHz => _diagPublishHz;

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
		set => _visualize = value;
	}

	public void SetSubParts(in bool value)
	{
		_devicePose.SubParts = value;
	}

	void Awake()
	{
		OnAwake();
		InitializeMessages();
	}

	void Start()
	{
		_devicePose.Store(this.transform);

		SetupMessages();

		StartCoroutine(DelayedStart());
	}

	protected virtual IEnumerator DelayedStart()
	{
		yield return new WaitForEndOfFrame();

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
			StartCoroutine(OnVisualize());
		}
	}

	protected void OnDestroy()
	{
		_running = false;

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
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"failed to PushDeviceMessage(): {ex.Message}");
			}
		}
	}

	/// <summary>
	/// Wake the TX thread immediately. Call from readback callbacks
	/// after enqueueing to _messageQueue so data is published with
	/// minimal latency instead of waiting for the next timer poll.
	/// </summary>
	protected void SignalDataReady()
	{
		_txDataReady.Set();
	}

	private IEnumerator DeviceCoroutineTx()
	{
		var waitForSeconds = new WaitForSeconds(WaitPeriod());
		while (_running)
		{
			GenerateMessage();
			yield return waitForSeconds;
		}
	}

	private IEnumerator DeviceCoroutineRx()
	{
		var waitUntil = new WaitUntil(() => (_deviceMessageQueue.Count > 0));
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
		// Event-driven TX loop.
		// Instead of sleeping for UpdatePeriod and hoping data is ready,
		// we block on _txDataReady which is signaled by readback callbacks.
		// This publishes data within <1ms of GPU readback completion.
		//
		// For high-rate sensors (>100 Hz, e.g. JointState at 1000 Hz),
		// WaitOne(1) has OS timer granularity (~1.3ms on Linux) which caps throughput.
		// We use a Stopwatch-based spin-yield loop for sub-10ms periods.
		//
		// NOTE: UpdateRate may be set AFTER the thread starts (e.g., from SDF config),
		// so timing parameters are re-evaluated dynamically inside the loop.

		_diagPublishSw.Start();

		// Timing parameters — recomputed when UpdateRate changes
		float lastUpdateRate = -1;
		long periodTicks = 0;
		bool useHighRes = false;
		long nextDeadline = 0;

		while (_running)
		{
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
				// Use high-res spin-wait for ≤10ms periods (≥100 Hz).
				// The 0.0101f threshold accommodates float imprecision at exactly 100 Hz.
				useHighRes = periodTicks > 0 && UpdatePeriod <= 0.0101f;
				nextDeadline = Stopwatch.GetTimestamp() + periodTicks;
			}

			if (!useHighRes)
			{
				// Standard event-driven path for ≥10ms periods (cameras, lidar, etc.)
				var timeoutMs = Mathf.Max(1, Mathf.RoundToInt(UpdatePeriod * 1000f));
				_txDataReady.WaitOne(timeoutMs);
			}

			GenerateMessage();

			if (useHighRes)
			{
				// Absolute-deadline spin wait: self-corrects drift.
				while (Stopwatch.GetTimestamp() < nextDeadline)
					Thread.SpinWait(1);
				nextDeadline += periodTicks;
				var now = Stopwatch.GetTimestamp();
				if (nextDeadline < now - 2 * periodTicks)
					nextDeadline = now + periodTicks;
			}

			// Periodic per-sensor Hz diagnostics
			var elapsed = (float)_diagPublishSw.Elapsed.TotalSeconds;
			if (elapsed >= DEVICE_DIAG_INTERVAL_SEC)
			{
				_diagPublishHz = _diagPublishCount / elapsed;
				Debug.Log($"[Device:{_deviceName}] publishHz={_diagPublishHz:F1} (target={UpdateRate:F0}) msgs={_diagPublishCount}/{elapsed:F1}s");
				_diagPublishCount = 0;
				_diagPublishSw.Restart();
			}
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
				Thread.SpinWait(1);
			}
		}
	}

	public bool PushDeviceMessage<T>(T instance) where T : global::ProtoBuf.IExtensible
	{
		try
		{
			if (!_deviceMessagePool.TryTake(out var deviceMessage))
			{
				deviceMessage = new DeviceMessage();
			}

			// Raw binary fast-path for image types — bypasses protobuf serialization
			if (instance is cloisim.msgs.ImageStamped imgStamped)
			{
				deviceMessage.SetRawImage(imgStamped);
			}
			else if (instance is cloisim.msgs.Segmentation seg)
			{
				deviceMessage.SetRawSegmentation(seg);
			}
			else if (instance is cloisim.msgs.ImagesStamped imgsStamped)
			{
				deviceMessage.SetRawImagesStamped(imgsStamped);
			}
			else
			{
				// Protobuf fallback for non-image sensors (lidar, IMU, etc.)
				deviceMessage.SetMessage<T>(instance);
			}

			var pushed = _deviceMessageQueue.Push(deviceMessage);
			if (pushed) Interlocked.Increment(ref _diagPublishCount);
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
		try
		{
			if (!_deviceMessagePool.TryTake(out var deviceMessage))
			{
				deviceMessage = new DeviceMessage();
			}
			if (deviceMessage.SetMessage(data))
			{
				return _deviceMessageQueue.Push(deviceMessage);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("ERROR: PushDeviceMessage(): " + ex.Message);
		}

		return false;
	}

	/// <summary>Return a DeviceMessage to the pool for reuse.</summary>
	public static void ReturnDeviceMessage(DeviceMessage msg)
	{
		if (msg != null)
		{
			msg.Reset();
			_deviceMessagePool.Add(msg);
		}
	}

	public bool PopDeviceMessage(out DeviceMessage data)
	{
		return _deviceMessageQueue.Pop(out data);
	}

	public void Reset()
	{
		// Debug.Log("Reset(): flush message queue");
		_messageQueue.Clear();
		_deviceMessageQueue.Flush();

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
				var clock = DeviceHelper.GetGlobalClock();
				_syntheticTime = (clock != null) ? clock.SimTime : Time.timeAsDouble;
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
