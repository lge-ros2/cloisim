/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Text;
using System;
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
	private static int _globalSequence = 0;
	public StringBuilder StartSummary { get; protected set; } = new();
	public bool IsStarted { get; private set; } = false;
	public event Action<CLOiSimPlugin> Started;

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

	protected List<TF> _staticTfList = new();

	private Pose _pluginPose = Pose.identity;

	private SDF.Plugin _pluginParameters = null;

	private List<ushort> _allocatedDevicePorts = new();
	private List<string> _allocatedDeviceHashKeys = new();

	protected abstract void OnAwake();
	protected abstract IEnumerator OnStart();
	protected virtual void OnReset() { }

	/// <summary>
	/// This method should be called in Awake()
	/// </summary>
	protected virtual void OnPluginLoad() { }

	protected async void OnDestroy()
	{
		_thread.Dispose();
		await TryCompleteThreadShutdownAsync(joinTimeoutMs: 50);

		_transport.Dispose();

		DeregisterDevice(_allocatedDevicePorts, _allocatedDeviceHashKeys);
		// Debug.Log($"({type.ToString()}){name}, CLOiSimPlugin destroyed.");
	}

	private async Task TryCompleteThreadShutdownAsync(int joinTimeoutMs = 50)
	{
		while (true)
		{
			if (_thread.TryJoinStep(joinTimeoutMs))
				break;
	        await Task.Yield();
		}
        await Task.Yield();
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
					StartSummary.AppendLine($"Multiple Plugin detected in Model({modelLink.name}) => Set subparts name({SubPartsName})");
				}
			}
		}
	}

	void Awake()
	{
		SetCustomHandleRequestMessage();
		OnAwake();
	}

	// Start is called before the first frame update
	void Start()
	{
		OnPluginLoad();

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

		StartCoroutine(DelayedOnStart());
	}

	private IEnumerator DelayedOnStart()
	{
		var sequence = _globalSequence++;
		for (var i = 0; i < sequence; i++)
			yield return null;

		try
		{
			yield return OnStart();
		}
		finally
		{
			_thread.Start();

			IsStarted = true;
			Started?.Invoke(this);

			StartSummary.AppendLine($"modelName=[{_modelName}] partsName=[{_partsName}] parentLinkName=[{_parentLinkName}]");
		}
	}

	public void Reset()
	{
		OnReset();
	}

	public Pose GetPose()
	{
		return _pluginPose;
	}

	private void StorePose()
	{
		_pluginPose.position = transform.localPosition;
		_pluginPose.rotation = transform.localRotation;
		// Debug.Log(modelName + ":" + transform.name + ", " + _pluginPose.position + ", " + _pluginPose.rotation);
	}
}