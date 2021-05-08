/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;
using ProtoBuf;

public abstract class Device : MonoBehaviour
{
	public enum ModeType { NONE, TX, RX, TX_THREAD, RX_THREAD };
	private const int maxQueue = 5;

	public ModeType Mode = ModeType.NONE;

	private BlockingCollection<MemoryStream> outboundQueue_ = new BlockingCollection<MemoryStream>(maxQueue);

	protected int timeoutForOutboundQueueInMilliseconds = 100;

	private MemoryStream memoryStream_ = new MemoryStream();

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

	private	bool runningDevice = false;

	public float UpdateRate => updateRate;

	public float UpdatePeriod => 1f/UpdateRate;

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

	void Awake()
	{
		ResetDataStream();

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

	void OnDestroy()
	{
		runningDevice = false;

		switch (Mode)
		{
			case ModeType.TX:
				StopCoroutine(DeviceCoroutineTx());
				Debug.Log("Stop TX device coroutine " + name);
				break;

			case ModeType.RX:
				StopCoroutine(DeviceCoroutineRx());
				Debug.Log("Stop TX device coroutine " + name);
				break;

			case ModeType.TX_THREAD:
				if (txThread != null && txThread.IsAlive)
				{
					txThread.Join();
					txThread.Abort();
					Debug.Log("Stop TX device thread " + name);
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
	}

	protected abstract void OnAwake();

	protected abstract void OnStart();

	protected virtual void ProcessDeviceCoroutine() { }

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
			ProcessDeviceCoroutine();
			GenerateMessage();
			yield return waitForSeconds;
		}
	}

	private IEnumerator DeviceCoroutineRx()
	{
		var waitUntil = new WaitUntil(() => GetDataStream().Length > 0);
		while (runningDevice)
		{
			yield return waitUntil;

			GenerateMessage();
			ProcessDeviceCoroutine();
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
			if (GetDataStream().Length > 0)
			{
				GenerateMessage();
			}
		}
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

	protected bool ResetDataStream()
	{
		if (memoryStream_ == null)
		{
			return false;
		}

		lock (memoryStream_)
		{
			memoryStream_.SetLength(0);
			memoryStream_.Position = 0;
			memoryStream_.Capacity = 0;

			return true;
		}
	}

	protected MemoryStream GetDataStream()
	{
		return (memoryStream_.CanRead) ? memoryStream_ : null;
	}

	public void SetDataStream(in byte[] data)
	{
		if (data == null)
		{
			return;
		}

		if (!ResetDataStream())
		{
			return;
		}

		lock (memoryStream_)
		{
			if (memoryStream_.CanWrite)
			{
				memoryStream_.Write(data, 0, data.Length);
				memoryStream_.Position = 0;
			}
			else
			{
				Debug.LogError("Failed to write memory stream");
			}
		}
	}

	protected void SetMessageData<T>(T instance)
	{
		if (memoryStream_ == null)
		{
			Debug.LogError("Cannot set data stream... it's null");
			return;
		}

		ResetDataStream();

		lock (memoryStream_)
		{
			Serializer.Serialize<T>(memoryStream_, instance);
		}
	}

	protected T GetMessageData<T>()
	{
		if (memoryStream_ == null)
		{
			Debug.LogError("Cannot Get data message... it's null");
			return default(T);
		}

		T result;

		lock (memoryStream_)
		{
			result = Serializer.Deserialize<T>(memoryStream_);
		}

		ResetDataStream();

		return result;
	}

	protected bool PushData<T>(T instance)
	{
		SetMessageData<T>(instance);
		return PushData();
	}

	protected bool PushData()
	{
		if (outboundQueue_ == null)
		{
			return false;
		}

		if (outboundQueue_.Count >= maxQueue)
		{
			// Debug.LogWarningFormat("Outbound queue is reached to maximum capacity({0})!!", maxQueue);

			while (outboundQueue_.Count > maxQueue/2)
			{
				PopData();
			}
		}

		if (!outboundQueue_.TryAdd(GetDataStream(), timeoutForOutboundQueueInMilliseconds))
		{
			Debug.LogWarningFormat("failed to add at " + deviceName);
			return false;
		}

		return true;
	}

	public MemoryStream PopData()
	{
		if (outboundQueue_ != null)
		{
			if (outboundQueue_.TryTake(out var item, timeoutForOutboundQueueInMilliseconds))
			{
				return item;
			}
		}

		return null;
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