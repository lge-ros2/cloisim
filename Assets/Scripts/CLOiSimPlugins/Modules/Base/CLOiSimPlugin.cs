/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public interface ICLOiSimPlugin
{
	enum Type {
		NONE,
		WORLD, GROUNDTRUTH, ELEVATOR, ACTOR,
		MICOM, JOINTCONTROL,
		SENSOR, GPS, IMU, IR, SONAR, CONTACT, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE, SEGMENTCAMERA
	};
	void SetPluginParameters(in SDF.Plugin node);
	SDF.Plugin GetPluginParameters();
	void Reset();
}

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	[field: SerializeField]
	protected ICLOiSimPlugin.Type _type { get; set; }

	[SerializeField]
	protected string _modelName = string.Empty;

	[SerializeField]
	protected string _partsName = string.Empty;

	public string ModelName
	{
		get => _modelName;
		set => _modelName = value;
	}

	public string PartsName
	{
		get => _partsName;
		set => _partsName = value;
	}

	protected string _parentLinkName = string.Empty;

	protected List<TF> staticTfList = new List<TF>();

	private Pose pluginPose = Pose.identity;

	private SDF.Plugin _pluginParameters = null;

	private List<ushort> _allocatedDevicePorts = new List<ushort>();
	private List<string> _allocatedDeviceHashKeys = new List<string>();

	protected List<Device> _attachedDevices = new List<Device>();

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() { }

	/// <summary>
	/// This method should be called in Awake()
	/// </summary>
	protected virtual void OnPluginLoad() { }

	protected void OnDestroy()
	{
		_thread.Dispose();
		_transport.Dispose();

		DeregisterDevice(_allocatedDevicePorts, _allocatedDeviceHashKeys);
		// Debug.Log($"({type.ToString()}){name}, CLOiSimPlugin destroyed.");
	}

	public void ChangePluginType(in ICLOiSimPlugin.Type targetType)
	{
		_type = targetType;
	}

	public void SetPluginParameters(in SDF.Plugin plugin)
	{
		_pluginParameters = plugin;
	}

	public SDF.Plugin GetPluginParameters()
	{
		return _pluginParameters;
	}

	private void StorePluginParametersInAttachedDevices()
	{
		foreach (var device in _attachedDevices)
		{
			device?.SetPluginParameters(_pluginParameters);
		}
	}

	private void DetectMultiplePlugin()
	{
		if (GetType() == typeof(LaserPlugin) ||
			GetType() == typeof(SonarPlugin) ||
			GetType() == typeof(RangePlugin) ||
			GetType() == typeof(IRPlugin) ||
			GetType() == typeof(ContactPlugin))
		{
			var helperLink = this.GetComponentInParent<SDF.Helper.Link>();
			var modelLink = (helperLink != null) ? helperLink.Model : null;

			if (modelLink != null)
			{
				var plugins = modelLink.GetComponentsInChildren<CLOiSimPlugin>();
				if (plugins.Length > 1)
				{
					SubPartsName = name;
					Debug.LogWarningFormat("Multiple Plugin detected in Model({0}) => Set subparts name({1})",
						modelLink.name, SubPartsName);
				}
			}
		}
	}

	void Awake()
	{
		SetCustomHandleRequestMessage();

		OnAwake();

		StorePluginParametersInAttachedDevices();

		OnPluginLoad();
	}

	// Start is called before the first frame update
	void Start()
	{
		DetectMultiplePlugin();

		StorePose();

		if (string.IsNullOrEmpty(_modelName))
		{
			_modelName = DeviceHelper.GetModelName(gameObject);
		}

		if (string.IsNullOrEmpty(_partsName))
		{
			_partsName = DeviceHelper.GetPartsName(gameObject);
		}

		var helperLink = this.GetComponentInParent<SDF.Helper.Link>();
		if (helperLink != null)
		{
			_parentLinkName = string.IsNullOrEmpty(helperLink.JointParentLinkName) ? null : helperLink.JointParentLinkName;
		}
		Debug.Log($"modelName={_modelName} partsName={_partsName} parentLinkName={_parentLinkName}");

		OnStart();

		_thread.Start();
	}

	public void Reset()
	{
		foreach (var device in _attachedDevices)
		{
			device?.Reset();
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