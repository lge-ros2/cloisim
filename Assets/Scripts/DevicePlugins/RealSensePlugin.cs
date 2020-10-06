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

	protected override void OnAwake()
	{
		type = Type.REALSENSE;
		cameras = GetComponentsInChildren<Camera>();
		partName = name;
	}

	protected override void OnStart()
	{
		var colorName = parameters.GetValue<string>("activate/module[@name='color']");
		var leftImagerName = parameters.GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = parameters.GetValue<string>("activate/module[@name='right_imager']");
		var depthName = parameters.GetValue<string>("activate/module[@name='depth']");

		if (colorName != null)
		{
			FindAndAddPlugin(colorName);
		}

		if (leftImagerName != null)
		{
			FindAndAddPlugin(leftImagerName);
		}

		if (rightImagerName != null)
		{
			FindAndAddPlugin(rightImagerName);
		}

		if (depthName != null)
		{
			FindAndAddPlugin(depthName);
		}

		RegisterServiceDevice("Info");

		AddThread(Response);
	}

	private void FindAndAddPlugin(in string name)
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
				break;
			}
		}
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
					case "request_module_list":
						SetModuleListInfoResponse(ref msForInfoResponse, activatedModules);
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

	protected static void SetModuleListInfoResponse(ref MemoryStream msModuleInfo, in List<string> modules)
	{
		if (msModuleInfo == null)
		{
			return;
		}

		var modulesInfo = new messages.Param();
		modulesInfo.Name = "activated_modules";
		modulesInfo.Value = new Any { Type = Any.ValueType.None };

		foreach (var module in modules)
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