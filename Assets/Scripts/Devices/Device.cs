/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;
using ProtoBuf;

public class DeviceMessage : MemoryStream
{
	public DeviceMessage()
	{
		Reset();
	}

	public DeviceMessage GetMessage()
	{
		return (CanRead) ? this : null;
	}

	public void GetMessage(out DeviceMessage message)
	{
		message = (CanRead) ? this : null;
	}

	public bool SetMessage(in byte[] data)
	{
		if (data == null)
		{
			return false;
		}

		Reset();

		lock (this)
		{
			if (CanWrite)
			{
				Write(data, 0, data.Length);
				Position = 0;
			}
			else
			{
				Debug.LogError("Failed to write memory stream");
			}
		}
		return true;
	}

	public void SetMessage<T>(T instance)
	{
		Reset();

		lock (this)
		{
			Serializer.Serialize<T>(this, instance);
		}
	}

	public T GetMessage<T>()
	{
		T result;

		lock (this)
		{
			result = Serializer.Deserialize<T>(this);
		}

		Reset();

		return result;
	}

	public void Reset()
	{
		lock (this)
		{
			SetLength(0);
			Position = 0;
			Capacity = 0;
		}
	}
}

public class DeviceMessageQueue : BlockingCollection<DeviceMessage>
{
	private const int MaxQueue = 5;
	private const int TimeoutFordeviceMessageQueueInMilliseconds = 100;

	public DeviceMessageQueue()
		: base(MaxQueue)
	{
	}

	public void Flush()
	{
		while (Count > 0)
		{
			Pop(out var item);
		}
	}

	private void FlushHalf()
	{
		while (Count > MaxQueue / 2)
		{
			Pop(out var item);
		}
	}

	public bool Push(in DeviceMessage data)
	{
		if (Count >= MaxQueue)
		{
			// Debug.LogWarningFormat("Outbound queue is reached to maximum capacity({0})!!", maxQueue);
			FlushHalf();
		}

		if (TryAdd(data, TimeoutFordeviceMessageQueueInMilliseconds))
		{
			return true;
		}

		return false;
	}

	public bool Pop(out DeviceMessage item)
	{
		if (TryTake(out item, TimeoutFordeviceMessageQueueInMilliseconds))
		{
			return true;
		}

		return false;
	}
}


public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };

	public ModeType Mode = ModeType.NONE;

	private DeviceMessageQueue deviceMessageQueue = new DeviceMessageQueue();
	private DeviceMessage deviceMessage = new DeviceMessage();

	public string deviceName = string.Empty;

	protected SDF.SensorType deviceParameters = null;
	private SDF.Helper.PluginParameters pluginParameters = null;

	private float updateRate = 1;

	private bool debugginOn = true;
	private bool visualize = true;

	private float transportingTimeSeconds = 0;

	[Range(0, 1.0f)]
	public float waitingPeriodRatio = 1.0f;

	private Pose deviceModelPose = Pose.identity;
	private Pose deviceLinkPose = Pose.identity;
	private Pose devicePose = Pose.identity;

	private Thread txThread = null;
	private Thread rxThread = null;

	private bool runningDevice = false;

	public float UpdateRate => updateRate;

	public float UpdatePeriod => 1f / UpdateRate;

	public float TransportingTime => transportingTimeSeconds;

	public bool EnableDebugging
	{
		get => debugginOn;
		set => debugginOn = value;
	}

	public bool EnableVisualize
	{
		get => visualize;
		set => visualize = value;
	}

	public bool IsDeviceRunning => runningDevice;

	void Awake()
	{
		OnAwake();
	}

	void Start()
	{
		StorePose();

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
				Debug.LogWarning("Device Mode is None");
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

	protected abstract void OnStart();

	protected virtual void ProcessDevice() { }

	protected virtual IEnumerator OnVisualize()
	{
		yield return null;
	}

	protected abstract void InitializeMessages();

	protected abstract void GenerateMessage();

	private IEnumerator DeviceCoroutineTx()
	{
		var waitForSeconds = new WaitForSeconds(WaitPeriod());
		while (runningDevice)
		{
			ProcessDevice();
			GenerateMessage();
			yield return waitForSeconds;
		}
	}

	private IEnumerator DeviceCoroutineRx()
	{
		var waitUntil = new WaitUntil(() => deviceMessage.Length > 0);
		while (runningDevice)
		{
			yield return waitUntil;
			GenerateMessage();
			ProcessDevice();
		}
	}

	private void DeviceThreadTx()
	{
		while (runningDevice)
		{
			ProcessDevice();
			GenerateMessage();
			Thread.Sleep(WaitPeriodInMilliseconds());
		}
	}

	private void DeviceThreadRx()
	{
		while (runningDevice)
		{
			if (deviceMessage.Length > 0)
			{
				GenerateMessage();
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
		deviceMessage.SetMessage(data);
		deviceMessage.GetMessage(out var message);
		return deviceMessageQueue.Push(message);
	}

	public bool PopDeviceMessage<T>(out T instance)
	{
		var result = deviceMessageQueue.Pop(out var data);
		instance = data.GetMessage<T>();
		return result;
	}

	public bool PopDeviceMessage(out DeviceMessage data)
	{
		return deviceMessageQueue.Pop(out data);
	}

	public void FlushDeviceMessageQueue()
	{
		deviceMessageQueue.Flush();
	}

	protected float WaitPeriod(in float messageGenerationTime = 0)
	{
		var waitTime = (UpdatePeriod * waitingPeriodRatio) - messageGenerationTime - TransportingTime;
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

	private void StorePose()
	{
		// Debug.Log(deviceName + ":" + transform.name);
		var devicePosition = Vector3.zero;
		var deviceRotation = Quaternion.identity;

		var parentLinkObject = transform.parent;
		if (parentLinkObject != null && parentLinkObject.CompareTag("Link"))
		{
			deviceLinkPose.position = parentLinkObject.localPosition;
			deviceLinkPose.rotation = parentLinkObject.localRotation;
			// Debug.Log(parentLinkObject.name + ": " + deviceLinkPose.position.ToString("F4") + ", " + deviceLinkPose.rotation.ToString("F4"));

			var parentModelObject = parentLinkObject.parent;
			if (parentModelObject != null && parentModelObject.CompareTag("Model"))
			{
				deviceModelPose.position = parentModelObject.localPosition;
				deviceModelPose.rotation = parentModelObject.localRotation;
				// Debug.Log(parentModelObject.name + ": " + deviceModelPose.position.ToString("F4") + ", " + deviceModelPose.rotation.ToString("F4"));
			}
		}

		devicePose.position = transform.localPosition;
		devicePose.rotation = transform.localRotation;
	}

	public Pose GetPose(in bool includingParent = true)
	{
		var finalPose = devicePose;

		if (includingParent)
		{
			finalPose.position += deviceLinkPose.position;
			finalPose.rotation *= deviceLinkPose.rotation;

			finalPose.position += deviceModelPose.position;
			finalPose.rotation *= deviceModelPose.rotation;
		}
		// Debug.Log(name + ": " + finalPose.position.ToString("F4") + ", " + finalPose.rotation.ToString("F4"));

		return finalPose;
	}

	public void SetPluginParameter(in SDF.Helper.PluginParameters pluginParams)
	{
		pluginParameters = pluginParams;
	}

	protected SDF.Helper.PluginParameters GetPluginParameters()
	{
		return pluginParameters;
	}

	public void SetDeviceParameter(in SDF.SensorType deviceParams)
	{
		deviceParameters = deviceParams;
	}

	public SDF.SensorType GetDeviceParameter()
	{
		return deviceParameters;
	}
}