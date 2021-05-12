/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using System.Xml;
using System.IO;
using System;
using UnityEngine;

public interface ICLOiSimPlugin
{
	void SetPluginName(in string name);
	void SetPluginParameters(in XmlNode node);
	void Reset();
}

public abstract partial class CLOiSimPlugin : CommonThread, ICLOiSimPlugin
{
	public enum Type {WORLD, ELEVATOR, MICOM, GPS, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE};

	public Type type { get; protected set; }

	private static BridgeManager bridgeManager = null;

	public string modelName { get; protected set; } = string.Empty;
	public string partName { get; protected set; } = string.Empty;

	private Pose pluginPose = Pose.identity;

	public string pluginName { get; protected set; } = string.Empty;
	protected SDF.Helper.PluginParameters parameters = new SDF.Helper.PluginParameters();

	private List<string> hashKeyList = new List<string>();

	private Device device = null;

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() {}

	public void SetDevice(in Device device)
	{
		this.device = device;
	}

	public void ChangePluginType(in CLOiSimPlugin.Type targetType)
	{
		type = targetType;
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

	private bool PrepareDevice(in string subPartName, out ushort port, out ulong hash)
	{
		if (bridgeManager.AllocateDevice(type.ToString(), modelName, partName, subPartName, out var hashKey, out port))
		{
			hashKeyList.Add(hashKey);

			hash = DeviceHelper.GetStringHashCode(hashKey);
			// Debug.LogFormat("PrepareDevice - port({0}) hash({1})", port, hash);
			return true;
		}

		Debug.LogError("Port for device is not allocated!!!!!!!! - " + hashKey);
		hash = 0;
		return false;
	}

	protected bool DeregisterDevice(in string hashKey)
	{
		bridgeManager.DeallocateDevice(hashKey);
		return true;
	}

	protected bool RegisterTxDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			SetHashForPublish(hash);
			InitializePublisher(port);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Tx device {0}, {1}", modelName, partName);

		return false;
	}

	protected bool RegisterRxDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			SetHashForSubscription(hash);
			InitializeSubscriber(port);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Rx device {0}, {1}", modelName, partName);

		return false;
	}

	protected bool RegisterServiceDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			SetHashForResponse(hash);
			InitializeResponsor(port);
			return true;
		}

		Debug.LogErrorFormat("Failed to register service device {0}, {1}", modelName, partName);

		return false;
	}

	protected bool RegisterClientDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			SetHashForRequest(hash);
			InitializeRequester(port);
			return true;
		}

		Debug.LogErrorFormat("Failed to register client device {0}, {1}", modelName, partName);

		return false;
	}

	void Awake()
	{
		var coreObject = GameObject.Find("Core");
		if (coreObject == null)
		{
			Debug.LogError("Failed to Find 'Core'!!!!");
		}
		else
		{
			if (bridgeManager == null)
			{
				bridgeManager = coreObject.GetComponent<BridgeManager>();
				if (bridgeManager == null)
				{
					Debug.LogError("Failed to get 'bridgeManager'!!!!");
				}
			}
		}

		OnAwake();
	}

	// Start is called before the first frame update
	void Start()
	{
		StorePose();

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

	public void Reset()
	{
		OnReset();
	}

	protected new void OnDestroy()
	{
		base.OnDestroy();

		DestroyTransporter();

		foreach (var hashKey in hashKeyList)
		{
			DeregisterDevice(hashKey);
		}

		Debug.Log(name + ", CLOiSimPlugin destroyed !!!!!!!!!!!");
	}

	public Pose GetPose()
	{
		return pluginPose;
	}

	private void StorePose()
	{
		// Debug.Log(deviceName + ":" + transform.name);
		pluginPose.position = transform.localPosition;
		pluginPose.rotation = transform.localRotation;
	}

	protected static void ClearDeviceMessage(ref DeviceMessage dm)
	{
		dm.Reset();
	}
}