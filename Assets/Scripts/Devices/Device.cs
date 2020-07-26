/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine;
using ProtoBuf;

public abstract class Device : MonoBehaviour
{
	public string deviceName = string.Empty;

	private const int maxQueue = 3;
	private BlockingCollection<MemoryStream> memoryStreamOutboundQueue;

	private MemoryStream memoryStream = null;

	protected const float SEC2MSEC = 1000.0f;

	private float updateRate = 1;

	private bool debugginOn = true;

	private bool visualize = true;

	private float transportingTimeSeconds = 0;

	void OnDestroy()
	{
		if (memoryStreamOutboundQueue.IsCompleted)
		{
			memoryStreamOutboundQueue.Dispose();
		}
	}

	public float UpdateRate => updateRate;

	public float UpdatePeriod => 1f/updateRate;

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
		memoryStreamOutboundQueue = new BlockingCollection<MemoryStream>(maxQueue);
		memoryStream = new MemoryStream();
		ResetDataStream();

		OnAwake();
	}

	void Start()
	{
		InitializeMessages();

		OnStart();

		StartCoroutine(MainDeviceWorker());

		if (EnableVisualize)
		{
			StartCoroutine(OnVisualize());
		}
	}

	protected abstract void OnAwake();

	protected abstract void OnStart();

	protected abstract IEnumerator MainDeviceWorker();

	protected abstract IEnumerator OnVisualize();

	protected abstract void InitializeMessages();

	protected abstract void GenerateMessage();

	protected float WaitPeriod(in float messageGenerationTime = 0)
	{
		var waitTime = UpdatePeriod - messageGenerationTime - TransportingTime;
		// Debug.LogFormat(deviceName + ": waitTime({0}) = period({1}) - elapsedTime({2}) - TransportingTime({3})",
		// 	waitTime.ToString("F5"), UpdatePeriod.ToString("F5"), messageGenerationTime.ToString("F5"), TransportingTime.ToString("F5"));
		return (waitTime < 0)? 0:waitTime;
	}

	public void SetUpdateRate(in float value)
	{
		updateRate = value;
	}

	public void SetTransportTime(in float value)
	{
		transportingTimeSeconds = value;
	}

	protected bool ResetDataStream()
	{
		lock (memoryStream)
		{
			if (memoryStream == null)
				return false;

			memoryStream.SetLength(0);
			memoryStream.Position = 0;
			memoryStream.Capacity = 0;

			return true;
		}
	}

	protected MemoryStream GetDataStream()
	{
		if (memoryStream.CanRead)
		{
			return memoryStream;
		}
		else
		{
			return null;
		}
	}

	public void SetDataStream(in byte[] data)
	{
		lock (memoryStream)
		{
			if (ResetDataStream())
			{
				if (data != null)
				{
					if (memoryStream.CanWrite)
					{
						memoryStream.Write(data, 0, data.Length);
						memoryStream.Position = 0;
					}
					else
						Debug.LogError("Failed to write memory stream");
				}
			}
		}
	}

	protected void SetMessageData<T>(T instance)
	{
		lock (memoryStream)
		{
			if (memoryStream == null)
			{
				Debug.LogError("Cannot set data stream... it's null");
				return;
			}

			ResetDataStream();
			Serializer.Serialize<T>(memoryStream, instance);
		}
	}

	protected T GetMessageData<T>()
	{
		lock (memoryStream)
		{
			if (memoryStream == null)
			{
				Debug.LogError("Cannot Get data message... it's null");
				return default(T);
			}

			T result = Serializer.Deserialize<T>(memoryStream);
			ResetDataStream();
			return result;
		}
	}

	protected bool PushData<T>(T instance)
	{
		SetMessageData<T>(instance);
		return PushData();
	}

	protected bool PushData()
	{
		if (memoryStreamOutboundQueue == null)
		{
			return false;
		}

		if (memoryStreamOutboundQueue.Count > maxQueue)
		{
			Debug.LogWarningFormat("Outbound queue is reached to maximum capacity({0})!!", maxQueue);
		}

		return memoryStreamOutboundQueue.TryAdd(GetDataStream());
	}

	public MemoryStream PopData()
	{
		if (memoryStreamOutboundQueue == null || memoryStreamOutboundQueue.Count == 0)
		{
			return null;
		}

		var item = memoryStreamOutboundQueue.Take();
		return item;
	}
}