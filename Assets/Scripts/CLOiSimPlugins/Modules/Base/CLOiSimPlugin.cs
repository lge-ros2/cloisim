/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System;
using UnityEngine;

public interface ICLOiSimPlugin
{
	enum Type {
		NONE,
		WORLD, GROUNDTRUTH, ELEVATOR, ACTOR,
		MICOM, JOINTCONTROL,
		SENSOR, GPS, IMU, IR, SONAR, CONTACT, LASER, CAMERA, DEPTHCAMERA, MULTICAMERA, REALSENSE, SEGMENTCAMERA, LOGICALCAMERA
	};
	void SetPluginParameters(in SDFormat.Plugin node);
	SDFormat.Plugin GetPluginParameters();
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

	private SDFormat.Plugin _pluginParameters = null;

	private List<ushort> _allocatedDevicePorts = new();
	private List<string> _allocatedDeviceHashKeys = new();

	protected abstract void OnAwake();
	protected abstract IEnumerator OnStart();
	protected virtual void OnReset() { }

	/// <summary>
	/// This method should be called in Awake()
	/// </summary>
	protected virtual void OnPluginLoad() { }

	/// <summary>
	/// Request this plugin's background threads to stop, without blocking or
	/// disposing transport/ports. Mirrors Device.RequestStop(). Call on every
	/// plugin in a subtree BEFORE destroying any of them (see
	/// Main.StopPluginWorkers) so all of their threads observe the stop flag and
	/// start exiting concurrently — otherwise each plugin's OnDestroy calls
	/// RequestStop() one at a time, and TryJoinStep's ~100ms poll wait stacks
	/// once per plugin instead of overlapping.
	/// </summary>
	public void RequestThreadStop()
	{
		_thread?.RequestStop();
	}

	/// <summary>
	/// Stops this plugin's background threads without disposing transport/ports.
	/// Unity does not guarantee OnDestroy() call order across independent
	/// GameObjects during application quit, so Main.OnDestroy's NetMQConfig.Cleanup()
	/// can otherwise run while this plugin's thread (e.g. PublishTfThread) is still
	/// mid-Send on its own NetMQ socket, tearing the shared context out from under
	/// a live native call (SIGSEGV). Main.OnApplicationQuit calls this on every
	/// plugin first, since OnApplicationQuit is guaranteed to run before any
	/// OnDestroy() during quit.
	/// </summary>
	public void StopThreadsForApplicationQuit()
	{
		_thread.Dispose();
		_thread.TryJoinStep(joinTimeoutMs: 500);
	}

	protected void OnDestroy()
	{
		// Suppress FreezeWatchdog for the duration of this intentional blocking join: deleting
		// a model tears down every plugin's threads in the same OnDestroy pass, and each plugin
		// can block the main thread for up to 500ms. Left unsuppressed, the watchdog treats this
		// expected teardown stall (stacked across plugins/devices) as a real freeze and
		// force-exits the process.
		CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
		try
		{
			_thread.Dispose();
			if (!_thread.TryJoinStep(joinTimeoutMs: 500))
			{
				Debug.LogWarning($"[{name}] plugin threads are still running; skipping transport dispose during teardown.");
			}
			else
			{
				_transport.Dispose();
			}

			DeregisterDevice(_allocatedDevicePorts, _allocatedDeviceHashKeys);
			// Debug.Log($"({type.ToString()}){name}, CLOiSimPlugin destroyed.");
		}
		finally
		{
			CLOiSim.Diagnostics.FreezeWatchdog.Restore();
		}
	}

	public void ChangePluginType(in ICLOiSimPlugin.Type targetType)
	{
		_type = targetType;
	}

	public void SetPluginParameters(in SDFormat.Plugin plugin)
	{
		_pluginParameters = plugin;
	}

	public SDFormat.Plugin GetPluginParameters()
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
			var helperLink = GetComponentInParent<SDFormat.Helper.Link>();
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

		var helperLink = GetComponentInParent<SDFormat.Helper.Link>();
		if (helperLink != null)
		{
			_parentLinkName = ResolvePluginParentFrameName(helperLink);
		}

		StartCoroutine(DelayedOnStart());
	}

	private IEnumerator DelayedOnStart()
	{
		var sequence = _globalSequence++;
		for (var i = 0; i < sequence; i++)
			yield return null;

		yield return OnStart();

		_thread.Start();

		IsStarted = true;
		Started?.Invoke(this);

		StartSummary.AppendLine($"modelName=[{_modelName}] partsName=[{_partsName}] parentLinkName=[{_parentLinkName}]");
	}

	public void Reset()
	{
		OnReset();
	}

	public Pose GetPose()
	{
		return _pluginPose;
	}

	private static string ResolvePluginParentFrameName(in SDFormat.Helper.Link helperLink)
	{
		if (helperLink == null)
		{
			return null;
		}

		var parentFrameName = string.IsNullOrEmpty(helperLink.JointChildLinkName)
			? helperLink.name
			: helperLink.JointChildLinkName;

		parentFrameName = TF.NormalizeFrameId(parentFrameName);

		return string.IsNullOrEmpty(parentFrameName) ? null : parentFrameName;
	}

	private void StorePose()
	{
		_pluginPose.position = transform.localPosition;
		_pluginPose.rotation = transform.localRotation;
		// Debug.Log(modelName + ":" + transform.name + ", " + _pluginPose.position + ", " + _pluginPose.rotation);
	}
}