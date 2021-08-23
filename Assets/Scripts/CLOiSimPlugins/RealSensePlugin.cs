/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;
using UnityEngine;

public class RealSensePlugin : CLOiSimMultiPlugin
{
	private SensorDevices.Camera[] cameras = null;
	private List<string> activatedModules = new List<string>();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.REALSENSE;
		cameras = GetComponentsInChildren<SensorDevices.Camera>();
		partsName = name;
	}

	protected override void OnStart()
	{
		var depthScale = GetPluginParameters().GetValue<uint>("configuration/depth_scale", 1000);

		var colorName = GetPluginParameters().GetValue<string>("activate/module[@name='color']");
		var leftImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='right_imager']");
		var depthName = GetPluginParameters().GetValue<string>("activate/module[@name='depth']");

		if (colorName != null)
		{
			FindAndAddCameraPlugin(colorName);
		}

		if (leftImagerName != null)
		{
			FindAndAddCameraPlugin(leftImagerName);
		}

		if (rightImagerName != null)
		{
			FindAndAddCameraPlugin(rightImagerName);
		}

		if (depthName != null)
		{
			var plugin = FindAndAddCameraPlugin(depthName);
			if (plugin == null)
			{
				Debug.LogWarning(depthName + " plugin is not loaded.");
			}
			else
			{
				var depthCamera = plugin.GetDepthCamera();

				if (depthCamera != null)
				{
					depthCamera.ReverseDepthData(false);
					depthCamera.depthScale = depthScale;
				}
			}
		}

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
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
				activatedModules.Add(name);
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
			moduleInfo.Value = new Any { Type = Any.ValueType.String, StringValue = module };
			modulesInfo.Childrens.Add(moduleInfo);
		}

		msModuleInfo.SetMessage<messages.Param>(modulesInfo);
	}
}