/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;
using UnityEngine;

public class RealSensePlugin : CLOiSimMultiPlugin
{
	private SensorDevices.Camera[] cameras = null;
	private SensorDevices.IMU imu = null;
	private List<Tuple<string, string>> activatedModules = new List<Tuple<string, string>>();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.REALSENSE;
		cameras = GetComponentsInChildren<SensorDevices.Camera>();
		imu = GetComponentInChildren<SensorDevices.IMU>();
		partsName = name;
	}

	protected override void OnStart()
	{
		var depthScale = GetPluginParameters().GetValue<uint>("configuration/depth_scale", 1000);
		var colorName = GetPluginParameters().GetValue<string>("activate/module[@name='color']");
		var leftImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='right_imager']");
		var depthName = GetPluginParameters().GetValue<string>("activate/module[@name='depth']");
		var imuName = GetPluginParameters().GetValue<string>("activate/module[@name='imu']");

		if (!string.IsNullOrEmpty(colorName))
		{
			FindAndAddCameraPlugin(colorName);
		}

		if (!string.IsNullOrEmpty(leftImagerName))
		{
			FindAndAddCameraPlugin(leftImagerName);
		}

		if (!string.IsNullOrEmpty(rightImagerName))
		{
			FindAndAddCameraPlugin(rightImagerName);
		}

		if (!string.IsNullOrEmpty(depthName))
		{
			FindAndAddDepthCameraPlugin(depthName, depthScale);
		}

		if (!string.IsNullOrEmpty(imuName))
		{
			AddImuPlugin(imuName);
		}

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}
	}

	private void AddImuPlugin(in string name)
	{
		if (imu.name.Equals(name))
		{
			var plugin = imu.gameObject.AddComponent<ImuPlugin>();
			plugin.ChangePluginType(ICLOiSimPlugin.Type.REALSENSE);
			plugin.SubPartsName = name;

			imu.SetSubParts(true);

			AddCLOiSimPlugin(name, plugin);
			activatedModules.Add(new Tuple<string, string>("imu", name));
		}
	}

	private void FindAndAddDepthCameraPlugin(in string name, in uint depthScale)
	{
		var plugin = FindAndAddCameraPlugin(name);
		if (plugin == null)
		{
			Debug.LogWarning(name + " plugin is not loaded.");
		}
		else
		{
			var depthCamera = plugin.GetDepthCamera();

			if (depthCamera != null)
			{
				depthCamera.SetDepthScale(depthScale);
			}
		}
	}

	private CameraPlugin FindAndAddCameraPlugin(in string name)
	{
		foreach (var camera in cameras)
		{
			if (camera.name.Equals(name))
			{
				var plugin = camera.gameObject.AddComponent<CameraPlugin>();
				plugin.ChangePluginType(ICLOiSimPlugin.Type.REALSENSE);
				plugin.SubPartsName = name;

				camera.SetSubParts(true);

				AddCLOiSimPlugin(name, plugin);
				activatedModules.Add(new Tuple<string, string>("camera", name));
				return plugin;
			}
		}

		return null;
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_module_list":
				SetModuleListInfoResponse(ref response);
				break;

			case "request_transform":
				var devicePose = GetPose();
				var deviceName = "RealSense";
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}

	private void SetModuleListInfoResponse(ref DeviceMessage msModuleInfo)
	{
		if (msModuleInfo == null)
		{
			return;
		}

		var modulesInfo = new messages.Param();
		modulesInfo.Name = "activated_modules";
		modulesInfo.Value = new Any { Type = Any.ValueType.None };

		foreach (var module in activatedModules)
		{
			var moduleInfo = new messages.Param();
			moduleInfo.Name = "module";
			moduleInfo.Value = new Any { Type = Any.ValueType.None };

			var moduleType = new messages.Param();
			moduleType.Name = "type";
			moduleType.Value = new Any { Type = Any.ValueType.String, StringValue = module.Item1 };
			moduleInfo.Childrens.Add(moduleType);

			var moduleValue = new messages.Param();
			moduleValue.Name = "name";
			moduleValue.Value = new Any { Type = Any.ValueType.String, StringValue = module.Item2 };
			moduleInfo.Childrens.Add(moduleValue);

			modulesInfo.Childrens.Add(moduleInfo);
		}

		msModuleInfo.SetMessage<messages.Param>(modulesInfo);
	}
}