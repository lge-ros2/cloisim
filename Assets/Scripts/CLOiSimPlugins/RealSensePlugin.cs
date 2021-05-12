/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ProtoBuf;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class RealSensePlugin : CLOiSimMultiPlugin
{
	private Camera[] cameras = null;
	private List<string> activatedModules = new List<string>();

	protected override void OnAwake()
	{
		type = Type.REALSENSE;
		cameras = GetComponentsInChildren<Camera>();
		partName = name;
	}

	protected override void OnStart()
	{
		var depthScale = parameters.GetValue<uint>("configuration/depth_scale", 1000);

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
				depthCamera.depthScale = depthScale;
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
				plugin.ChangePluginType(CLOiSimPlugin.Type.REALSENSE);
				plugin.subPartName = name;

				AddCLOiSimPlugin(name, plugin);
				activatedModules.Add(name);
				return plugin;
			}
		}

		return null;
	}

	private void Response()
	{
		var dmInfoResponse = new DeviceMessage();
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref dmInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_module_list":
						SetModuleListInfoResponse(ref dmInfoResponse);
						break;

					case "request_transform":
						var devicePose = GetPose();
						SetTransformInfoResponse(ref dmInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(dmInfoResponse);
			}

			WaitThread();
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