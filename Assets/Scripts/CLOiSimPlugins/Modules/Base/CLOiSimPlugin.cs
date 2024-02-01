/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public interface ICLOiSimPlugin
{
	enum Type { WORLD, GROUNDTRUTH, ELEVATOR, ACTOR, MICOM, JOINTCONTROL, GPS, IMU, SONAR, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE };
	void SetPluginParameters(in SDF.Plugin node);
	SDF.Plugin GetPluginParameters();
	void Reset();
}

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	public ICLOiSimPlugin.Type type { get; protected set; }

	public string pluginName { get; set; } = string.Empty;
	public string modelName { get; protected set; } = string.Empty;
	public string partsName { get; protected set; } = string.Empty;

	protected List<TF> staticTfList = new List<TF>();

	private Pose pluginPose = Pose.identity;

	private SDF.Plugin pluginParameters;

	private List<ushort> allocatedDevicePorts = new List<ushort>();
	private List<string> allocatedDeviceHashKeys = new List<string>();

	protected Dictionary<string, Device> attachedDevices = new Dictionary<string, Device>();

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() { }

	protected void OnDestroy()
	{
		DeregisterDevice(allocatedDevicePorts, allocatedDeviceHashKeys);

		thread.Dispose();
		transport.Dispose();
		Debug.Log($"({type.ToString()}){name}, CLOiSimPlugin destroyed.");
	}

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

	void Awake()
	{
		SetCustomHandleRequestMessage();

		OnAwake();
	}

	// Start is called before the first frame update
	void Start()
	{
		foreach (var device in attachedDevices.Values)
		{
			device.SetPluginParameters(pluginParameters);
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

		thread.Start();
	}

	public void Reset()
	{
		foreach (var device in attachedDevices.Values)
		{
			device.Reset();
		}

		OnReset();
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