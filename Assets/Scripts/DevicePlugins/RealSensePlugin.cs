/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ProtoBuf;
using messages = gazebo.msgs;
using Any = gazebo.msgs.Any;

public class RealSensePlugin : DevicesPlugin
{
	private Camera[] cameras = null;
	private List<string> activatedModules = new List<string>();

	#region Parameters
	private double depthRangeMin;
	private double depthRangeMax;
	private uint depthScale;
	#endregion

	protected override void OnAwake()
	{
		type = Type.REALSENSE;
		cameras = GetComponentsInChildren<Camera>();
		partName = name;
	}

	protected override void OnStart()
	{
		depthScale = parameters.GetValue<uint>("configuration/depth_scale", 1000);

		var colorName = parameters.GetValue<string>("activate/module[@name='color']");
		var leftImagerName = parameters.GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = parameters.GetValue<string>("activate/module[@name='right_imager']");
		var depthName = parameters.GetValue<string>("activate/module[@name='depth']");

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
			var depthCamera = plugin.GetCamera() as SensorDevices.DepthCamera;

			if (depthCamera != null)
			{
				depthCamera.ReverseDepthData(false);
				depthRangeMin = depthCamera.GetParameters().clip.near;
				depthRangeMax = depthCamera.GetParameters().clip.far;
			}
		}

		RegisterServiceDevice("Info");

		AddThread(Response);
	}

	private CameraPlugin FindAndAddCameraPlugin(in string name)
	{
		foreach (var camera in cameras)
		{
			if (camera.gameObject.name.Equals(name))
			{
				var plugin = camera.gameObject.AddComponent<CameraPlugin>();
				plugin.ChangePluginType(DevicePlugin.Type.REALSENSE);
				plugin.subPartName = name;

				AddDevicePlugin(name, plugin);
				activatedModules.Add(name);
				return plugin;
			}
		}

		return null;
	}

	private void Response()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_realsense_parameters":
						SetConfigurationInfoResponse(ref msForInfoResponse);
						break;

					case "request_module_list":
						SetModuleListInfoResponse(ref msForInfoResponse);
						break;

					case "request_transform":
						var devicePose = GetPose();
						SetTransformInfoResponse(ref msForInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}

			ThreadWait();
		}
	}

	private void SetConfigurationInfoResponse(ref MemoryStream msModuleInfo)
	{
		if (msModuleInfo == null)
		{
			return;
		}

		var paramInfo = new messages.Param();
		var modulesInfo = new messages.Param();
		modulesInfo.Name = "parameters";
		modulesInfo.Value = new Any { Type = Any.ValueType.None };

		paramInfo.Name = "depth_range_min";
		paramInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = depthRangeMin };
		modulesInfo.Childrens.Add(paramInfo);

		paramInfo.Name = "depth_range_max";
		paramInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = depthRangeMax };
		modulesInfo.Childrens.Add(paramInfo);

		paramInfo.Name = "depth_scale";
		paramInfo.Value = new Any { Type = Any.ValueType.Int32, IntValue = (int)depthScale };
		modulesInfo.Childrens.Add(paramInfo);

		ClearMemoryStream(ref msModuleInfo);
		Serializer.Serialize<messages.Param>(msModuleInfo, modulesInfo);
	}

	private void SetModuleListInfoResponse(ref MemoryStream msModuleInfo)
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

		ClearMemoryStream(ref msModuleInfo);
		Serializer.Serialize<messages.Param>(msModuleInfo, modulesInfo);
	}
}