/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using System.Collections;
using UnityEngine;

public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };

	public ModeType Mode = ModeType.NONE;
	private DeviceMessageQueue deviceMessageQueue = new DeviceMessageQueue();
	private DeviceMessage deviceMessage = new DeviceMessage();
	private DevicePose devicePose = new DevicePose();

	private SDF.Plugin pluginParameters = null;

	private string deviceName = string.Empty;

	private float updateRate = 1;

	private bool debuggingOn = true;
	private bool visualize = true;

	private float transportingTimeSeconds = 0;

	private Thread txThread = null;
	private Thread rxThread = null;

	private bool runningDevice = false;

	public float UpdatePeriod => 1f / updateRate;

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
		devicePose.SubParts = value;
	}

	void Awake()
	{
		OnAwake();
	}

	void Start()
	{
		devicePose.Store(this.transform);

		InitializeMessages();

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

		deviceMessage.Dispose();
		deviceMessageQueue.Dispose();
	}

	protected abstract void OnAwake();

	protected virtual void OnStart() {}

	protected virtual void OnReset() {}


	protected virtual IEnumerator OnVisualize()
	{
		yield return null;
	}

	protected virtual void InitializeMessages() { }

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
		var waitUntil = new WaitUntil(() => deviceMessageQueue.Count > 0);
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
			if (deviceMessageQueue.Count > 0)
			{
				ProcessDevice();
			}
		}
	}

	public bool PushDeviceMessage<T>(T instance)
	{
		deviceMessage.SetMessage<T>(instance);
		deviceMessage.GetMessage(out var message);
		return deviceMessageQueue.Push(message);
	}

	public bool PushDeviceMessage(in byte[] data)
	{
		if (deviceMessage.SetMessage(data))
		{
			deviceMessage.GetMessage(out var message);
			return deviceMessageQueue.Push(message);
		}

		return false;
	}

	public bool PopDeviceMessage<T>(out T instance)
	{
		try
		{
			var result = deviceMessageQueue.Pop(out var data);
			instance = data.GetMessage<T>();
			return result;
		}
		catch
		{
			instance = default(T);
			Debug.LogWarning("PopDeviceMessage<T>(): ERROR");
		}

		return false;
	}

	public bool PopDeviceMessage(out DeviceMessage data)
	{
		return deviceMessageQueue.Pop(out data);
	}

	public void Reset()
	{
		// Debug.Log("Reset(): flush message queue");
		deviceMessage.Reset();
		deviceMessageQueue.Flush();

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
		updateRate = value;
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
		return devicePose.Get();
	}

	public void SetSubPartsPose(in string partsName, in Transform targetTransform)
	{
		devicePose.Store(partsName, targetTransform);
	}

	public Pose GetSubPartsPose(in string targetPartsName)
	{
		return devicePose.Get(targetPartsName);
	}
}