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
	void SetPluginParameters(in XmlNode node);
	void Reset();
}

public abstract partial class DevicePlugin : DeviceTransporter, IDevicePlugin
{
	public string modelName { get; protected set; } = String.Empty;
	public string partName { get; protected set; } = String.Empty;

	private BridgeManager bridgeManager = null;

	protected PluginParameters parameters = null;

	protected MemoryStream msForInfoResponse = new MemoryStream();

	private bool runningThread = true;
	private List<Thread> threadList = new List<Thread>();

	protected bool IsRunningThread => runningThread;

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected abstract void OnTerminate();
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

	protected string MakeHashKey(in string subPartName = "")
	{
		return modelName + partName + subPartName;
	}

	protected bool DeregisterDevice(in string hashKey)
	{
		bridgeManager.DeallocateDevicePort(hashKey);
		return true;
	}

	protected bool RegisterTxDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForPublish(hash);
			InitializePublisher(port);
			return true;
		}

		return false;
	}

	protected bool RegisterRxDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForSubscription(hash);
			InitializeSubscriber(port);
			return 	true;
		}

		return true;
	}

	protected bool RegisterServiceDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForResponse(hash);
			InitializeResponsor(port);

			return true;
		}

		return true;
	}

	protected bool RegisterClientDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out var port, out var hash))
		{
			SetHashForRequest(hash);
			InitializeRequester(port);
			return true;
		}

		return true;
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

		if (string.IsNullOrEmpty(modelName))
		{
			modelName = DeviceHelper.GetModelName(gameObject);
		}
	}

	// Start is called before the first frame update
	void Start()
	{
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

		OnTerminate();
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