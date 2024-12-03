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
	private DeviceMessage _deviceMessage = new DeviceMessage();
	private DevicePose _devicePose = new DevicePose();

	private SDF.Plugin pluginParameters = null;

	[SerializeField]
	private string deviceName = string.Empty;

	[SerializeField]
	private float _updateRate = 1;

	private bool debuggingOn = true;

	[SerializeField]
	private bool visualize = true;

	private float transportingTimeSeconds = 0;

	private Thread txThread = null;
	private Thread rxThread = null;

	private bool runningDevice = false;

	public float UpdatePeriod => 1f / UpdateRate;

	public float UpdateRate => _updateRate;

	public string DeviceName
	{
		get => deviceName;
		set => deviceName = value;
	}

	public bool EnableDebugging
	{
		get => debuggingOn;
		set => debuggingOn = value;
	}

	public bool EnableVisualize
	{
		get => visualize;
		set => visualize = value;
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

		runningDevice = true;

		switch (Mode)
		{
			case ModeType.TX:
				StartCoroutine(DeviceCoroutineTx());
				break;

			case ModeType.RX:
				StartCoroutine(DeviceCoroutineRx());
				break;

			case ModeType.TX_THREAD:
				txThread = new Thread(DeviceThreadTx);
				txThread.Start();
				break;

			case ModeType.RX_THREAD:
				rxThread = new Thread(DeviceThreadRx);
				rxThread.Start();
				break;

			case ModeType.NONE:
			default:
				runningDevice = false;
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
		runningDevice = false;

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
				if (txThread != null && txThread.IsAlive)
				{
					txThread.Join();
					txThread.Abort();
					Debug.Log("Stop TX device thread: " + name);
				}
				break;

			case ModeType.RX_THREAD:
				if (rxThread != null && rxThread.IsAlive)
				{
					rxThread.Join();
					rxThread.Abort();
					Debug.Log("Stop RX device thread: " + name);
				}
				break;

			case ModeType.NONE:
			default:
				break;
		}

		_deviceMessage.Dispose();
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
		while (runningDevice)
		{
			GenerateMessage();
			yield return waitForSeconds;
		}
	}

	private IEnumerator DeviceCoroutineRx()
	{
		var waitUntil = new WaitUntil(() => (_deviceMessageQueue.Count > 0));
		while (runningDevice)
		{
			yield return waitUntil;
			ProcessDevice();
		}
	}

	private void DeviceThreadTx()
	{
		while (runningDevice)
		{
			GenerateMessage();
			Thread.Sleep(WaitPeriodInMilliseconds());
		}
	}

	private void DeviceThreadRx()
	{
		while (runningDevice)
		{
			if (_deviceMessageQueue.Count > 0)
			{
				ProcessDevice();
			}
		}
	}

	public bool PushDeviceMessage<T>(T instance)
	{
		try
		{
			_deviceMessage.SetMessage<T>(instance);
			return _deviceMessageQueue.Push(_deviceMessage);
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
			if (_deviceMessage.SetMessage(data))
			{
				return _deviceMessageQueue.Push(_deviceMessage);
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
		_deviceMessage.Reset();
		_deviceMessageQueue.Flush();

		OnReset();
	}

	protected float WaitPeriod(in float messageGenerationTime = 0)
	{
		var waitTime = UpdatePeriod - messageGenerationTime - transportingTimeSeconds;
		// Debug.LogFormat(deviceName + ": waitTime({0}) = period({1}) - elapsedTime({2}) - TransportingTime({3})",
		// 	waitTime.ToString("F5"), UpdatePeriod.ToString("F5"), messageGenerationTime.ToString("F5"), TransportingTime.ToString("F5"));
		return (waitTime < 0) ? 0 : waitTime;
	}

	protected int WaitPeriodInMilliseconds()
	{
		return (int)(WaitPeriod() * 1000f);
	}

	public void SetUpdateRate(in float value)
	{
		_updateRate = value;
	}

	public void SetTransportedTime(in float value)
	{
		transportingTimeSeconds = value;
	}

	/// <summary>
	/// This method should be called in OnStart()
	/// </summary>
	public void SetPluginParameters(in SDF.Plugin plugin)
	{
		pluginParameters = plugin;
	}

	public SDF.Plugin GetPluginParameters()
	{
		return pluginParameters;
	}

	public Pose GetPose()
	{
		return _devicePose.Get();
	}
}