/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using System.IO;
using System;
using UnityEngine;
using System.Xml;

public interface IDevicePlugin
{
	void SetPluginName(in string name);
	void SetPluginParameters(in XmlNode node);
	void Reset();
}

public abstract partial class DevicePlugin : DeviceTransporter, IDevicePlugin
{
	public string modelName { get; protected set; } = string.Empty;
	public string partName { get; protected set; } = string.Empty;

	private BridgeManager bridgeManager = null;

	public string pluginName { get; protected set; } = string.Empty;
	protected PluginParameters parameters = null;

	protected MemoryStream msForInfoResponse = new MemoryStream();

	private bool runningThread = true;
	private List<Thread> threadList = new List<Thread>();
	private List<string> hashKeyList = new List<string>();

	protected bool IsRunningThread => runningThread;

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() {}

	protected bool AddThread(in ThreadStart function)
	{
		if (function != null)
		{
			threadList.Add(new Thread(function));
			// thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			return true;
		}

		return false;
	}

	private void StartThreads()
	{
		foreach (var thread in threadList)
		{
			if (thread != null && !thread.IsAlive)
			{
				thread.Start();
			}
		}
	}

	public void SetPluginName(in string name)
	{
		pluginName = name;
	}

	public void SetPluginParameters(in XmlNode node)
	{
		if (parameters != null)
		{
			parameters.SetRootData(node);
		}
		else
		{
			Debug.LogWarning("Cannot set plugin parameters");
		}
	}

	private bool PrepareDevice(in string hashKey, out ushort port, out ulong hash)
	{
		port = bridgeManager.AllocateDevicePort(hashKey);
		if (port == 0)
		{
			Debug.LogError("Port for device is not allocated!!!!!!!!");
			hash = 0;
			return false;
		}

		hash = DeviceHelper.GetStringHashCode(hashKey);
		// Debug.LogFormat("PrepareDevice - port({0}) hash({1})", port, hash);
		return true;
	}

	private string MakeHashKey(in string subPartName = "")
	{
		return modelName + partName + subPartName;
	}

	protected bool DeregisterDevice(in string hashKey)
	{
		bridgeManager.DeallocateDevicePort(hashKey);
		return true;
	}

	protected bool RegisterTxDevice(in string subHashKey = "")
	{
		var hashKey = MakeHashKey(subHashKey);

		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForPublish(hash);
			InitializePublisher(port);

			hashKeyList.Add(hashKey);

			return true;
		}

		Debug.LogError("Failed to register Tx Device - " + hashKey);

		return false;
	}

	protected bool RegisterRxDevice(in string subHashKey = "")
	{
		var hashKey = MakeHashKey(subHashKey);

		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForSubscription(hash);
			InitializeSubscriber(port);

			hashKeyList.Add(hashKey);

			return 	true;
		}

		Debug.LogError("Failed to register Rx Device - " + hashKey);

		return false;
	}

	protected bool RegisterServiceDevice(in string subHashKey = "")
	{
		var hashKey = MakeHashKey(subHashKey);

		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForResponse(hash);
			InitializeResponsor(port);

			hashKeyList.Add(hashKey);

			return true;
		}

		Debug.LogError("Failed to register service device - " + hashKey);

		return false;
	}

	protected bool RegisterClientDevice(in string subHashKey = "")
	{
		var hashKey = MakeHashKey(subHashKey);

		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForRequest(hash);
			InitializeRequester(port);

			hashKeyList.Add(hashKey);

			return true;
		}

		Debug.LogError("Failed to register client device - " + hashKey);

		return false;
	}

	void Awake()
	{
		InitializeTransporter();

		parameters = new PluginParameters();

		var coreObject = GameObject.Find("Core");
		if (coreObject == null)
		{
			Debug.LogError("Failed to Find 'Core'!!!!");
		}
		else
		{
			bridgeManager = coreObject.GetComponent<BridgeManager>();
			if (bridgeManager == null)
			{
				Debug.LogError("Failed to get 'bridgeManager'!!!!");
			}
		}

		OnAwake();
	}

	// Start is called before the first frame update
	void Start()
	{
		if (string.IsNullOrEmpty(modelName))
		{
			modelName = DeviceHelper.GetModelName(gameObject);
		}

		if (string.IsNullOrEmpty(partName))
		{
			partName = pluginName;
		}

		// PrintPluginData();

		OnStart();

		StartThreads();
	}

	void OnDestroy()
	{
		// Debug.Log("DevicePlugin destroied");
		runningThread = false;
		foreach (var thread in threadList)
		{
			if (thread != null)
			{
				if (thread.IsAlive)
				{
					thread.Join();
				}
			}
		}

		DestroyTransporter();

		foreach (var hashKey in hashKeyList)
		{
			DeregisterDevice(hashKey);
		}
	}

	public void Reset()
	{
		OnReset();
	}

	protected void ThreadWait()
	{
		Thread.SpinWait(1);
	}

	protected static void ClearMemoryStream(ref MemoryStream ms)
	{
		if (ms != null)
		{
			ms.SetLength(0);
			ms.Position = 0;
			ms.Capacity = 0;
		}
	}
}