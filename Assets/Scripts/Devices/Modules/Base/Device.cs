/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Threading;
using System.Collections;
using UnityEngine;

public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };
	public ModeType Mode = ModeType.NONE;

	private DeviceMessageQueue _deviceMessageQueue = new DeviceMessageQueue();
	private DevicePose _devicePose = new DevicePose();

	private SDF.Plugin _pluginParameters = null;

	[SerializeField]
	private string _deviceName = string.Empty;

	[SerializeField]
	private float _updateRate = 1;

	private bool _debuggingOn = true;

	[SerializeField]
	private bool _visualize = true;

	[SerializeField]
	private float _transportingTimeSeconds = 0;

	private Thread _txThread = null;
	private Thread _rxThread = null;

	private bool _running = false;

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

		OnStart();

		_running = true;

		switch (Mode)
		{
			case ModeType.TX:
				StartCoroutine(DeviceCoroutineTx());
				break;

			case ModeType.RX:
				StartCoroutine(DeviceCoroutineRx());
				break;

			case ModeType.TX_THREAD:
				_txThread = new Thread(DeviceThreadTx);
				_txThread.Start();
				break;

			case ModeType.RX_THREAD:
				_rxThread = new Thread(DeviceThreadRx);
				_rxThread.Start();
				break;

			case ModeType.NONE:
			default:
				_running = false;
				// Debug.LogWarning("Device(" + name + ") Mode is None");
				break;
		}

		if (EnableVisualize)
		{
			StartCoroutine(OnVisualize());
		}
	}

	protected void OnDestroy()
	{
		_running = false;

		switch (Mode)
		{
			case ModeType.TX:
				StopCoroutine(DeviceCoroutineTx());
				Debug.Log("Stop TX device coroutine: " + name);
				break;

			case ModeType.RX:
				StopCoroutine(DeviceCoroutineRx());
				Debug.Log("Stop TX device coroutine: " + name);
				break;

			case ModeType.TX_THREAD:
				if (_txThread != null && _txThread.IsAlive)
				{
					_txThread.Join();
					_txThread.Abort();
					Debug.Log("Stop TX device thread: " + name);
				}
				break;

			case ModeType.RX_THREAD:
				if (_rxThread != null && _rxThread.IsAlive)
				{
					_rxThread.Join();
					_rxThread.Abort();
					Debug.Log("Stop RX device thread: " + name);
				}
				break;

			case ModeType.NONE:
			default:
				break;
		}

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
	protected virtual void ProcessDevice() { }

	// Used for TX
	protected virtual void GenerateMessage() { }

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
			ProcessDevice();
		}
	}

	private void DeviceThreadTx()
	{
		while (_running)
		{
			GenerateMessage();
			Thread.Sleep(WaitPeriodInMilliseconds());
		}
	}

	private void DeviceThreadRx()
	{
		while (_running)
		{
			if (_deviceMessageQueue.Count > 0)
			{
				ProcessDevice();
				Thread.SpinWait(1);
			}
		}
	}

	public bool PushDeviceMessage<T>(T instance)
	{
		try
		{
			var deviceMessage = new DeviceMessage();
			deviceMessage.SetMessage<T>(instance);
			return _deviceMessageQueue.Push(deviceMessage);
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
			var deviceMessage = new DeviceMessage();
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

	public bool PopDeviceMessage<T>(out T instance)
	{
		try
		{
			var result = _deviceMessageQueue.Pop(out var data);
			instance = (result) ? data.GetMessage<T>() : default(T);
			return result;
		}
		catch (Exception ex)
		{
			instance = default(T);
			Debug.LogWarning($"ERROR: PopDeviceMessage<{typeof(T).ToString()}>(): {ex.Message}");
		}

		return false;
	}

	public bool PopDeviceMessage(out DeviceMessage data)
	{
		return _deviceMessageQueue.Pop(out data);
	}

	public void Reset()
	{
		// Debug.Log("Reset(): flush message queue");
		_deviceMessageQueue.Flush();

		OnReset();
	}

	protected float WaitPeriod(in float messageGenerationTime = 0)
	{
		var waitTime = UpdatePeriod - messageGenerationTime - _transportingTimeSeconds;
		// Debug.LogFormat(_deviceName + ": waitTime({0}) = period({1}) - elapsedTime({2}) - TransportingTime({3})",
		// 	waitTime.ToString("F5"), UpdatePeriod.ToString("F5"), messageGenerationTime.ToString("F5"), _transportingTimeSeconds.ToString("F5"));
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

	/// <summary>
	/// This method should be called in OnStart()
	/// </summary>
	public void SetPluginParameters(in SDF.Plugin plugin)
	{
		_pluginParameters = plugin;
	}

	public SDF.Plugin GetPluginParameters()
	{
		return _pluginParameters;
	}

	public Pose GetPose()
	{
		return _devicePose.Get();
	}
}