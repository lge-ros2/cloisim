/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public interface ICLOiSimPlugin
{
	enum Type {WORLD, GROUNDTRUTH, ELEVATOR, ACTOR, MICOM, GPS, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE};
	void SetPluginParameters(in SDF.Plugin node);
	SDF.Plugin GetPluginParameters();
	void Reset();
}

[DefaultExecutionOrder(560)]
public abstract partial class CLOiSimPlugin : CLOiSimPluginThread, ICLOiSimPlugin
{
	public ICLOiSimPlugin.Type type { get; protected set; }

	private static BridgeManager bridgeManager = null;

	public string pluginName { get; set; } = string.Empty;
	public string modelName { get; protected set; } = string.Empty;
	public string partsName { get; protected set; } = string.Empty;

	private Pose pluginPose = Pose.identity;

	private SDF.Plugin pluginParameters;

	private List<string> allocatedDeviceHashKeys = new List<string>();

	protected Device targetDevice = null;

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() {}

	public void ChangePluginType(in ICLOiSimPlugin.Type targetType)
	{
		type = targetType;
	}

	public void SetPluginParameters(in SDF.Plugin plugin)
	{
		pluginParameters = plugin;
	}

	public SDF.Plugin GetPluginParameters()
	{
		return pluginParameters;
	}

	private bool PrepareDevice(in string subPartName, out ushort port, out ulong hash)
	{
		if (bridgeManager.AllocateDevice(type.ToString(), modelName, partsName, subPartName, out var hashKey, out port))
		{
			allocatedDeviceHashKeys.Add(hashKey);

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
			InitializePublisher(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Tx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterRxDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			InitializeSubscriber(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Rx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterServiceDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			InitializeResponsor(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register service device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterClientDevice(in string subPartName = "")
	{
		if (PrepareDevice(subPartName, out var port, out var hash))
		{
			InitializeRequester(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register client device {0}, {1}", modelName, partsName);

		return false;
	}

	void Awake()
	{
		var coreObject = Main.CoreObject;
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
		if (targetDevice != null)
		{
			targetDevice.SetPluginParameters(pluginParameters);
		}

		StorePose();

		if (string.IsNullOrEmpty(modelName))
		{
			modelName = DeviceHelper.GetModelName(gameObject);
		}

		if (string.IsNullOrEmpty(partsName))
		{
			partsName = pluginParameters.Name;
		}

		OnStart();

		StartThreads();
	}

	public void Reset()
	{
		OnReset();
	}

	protected new void OnDestroy()
	{
		foreach (var hashKey in allocatedDeviceHashKeys)
		{
			DeregisterDevice(hashKey);
		}

		base.OnDestroy();
		// Debug.Log(name + ", CLOiSimPlugin destroyed !!!!!!!!!!!");
	}

	public Pose GetPose()
	{
		return pluginPose;
	}

	private void StorePose()
	{
		pluginPose.position = transform.localPosition;
		pluginPose.rotation = transform.localRotation;
		// Debug.Log(modelName + ":" + transform.name + ", " + pluginPose.position + ", " + pluginPose.rotation);
	}
}