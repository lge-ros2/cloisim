/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class RealSensePlugin : CLOiSimMultiPlugin
{
	private SensorDevices.Camera[] _cameras = null;
	private SensorDevices.IMU _imu = null;
	private List<Tuple<string, string>> _activatedModules = new List<Tuple<string, string>>();

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.REALSENSE;
		_partsName = name;

		_cameras = GetComponentsInChildren<SensorDevices.Camera>();
		_imu = GetComponentInChildren<SensorDevices.IMU>();
	}

	protected override void OnStart()
	{
		var colorName = GetPluginParameters().GetValue<string>("activate/module[@name='color']");
		var leftImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = GetPluginParameters().GetValue<string>("activate/module[@name='right_imager']");
		var depthName = GetPluginParameters().GetValue<string>("activate/module[@name='depth']");
		var alignedDepthToColorName = GetPluginParameters().GetValue<string>("activate/module[@name='aligned_depth_to_color']");
		var imuName = GetPluginParameters().GetValue<string>("activate/module[@name='imu']");

		// Important Register base parts first!
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

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
			FindAndAddCameraPlugin(depthName);
		}

		if (!string.IsNullOrEmpty(alignedDepthToColorName))
		{
			FindAndAddCameraPlugin(alignedDepthToColorName);
		}

		if (!string.IsNullOrEmpty(imuName))
		{
			AddImuPlugin(imuName);
		}
	}

	private void AddImuPlugin(in string name)
	{
		if (_imu.name.Equals(name))
		{
			_imu.SetSubParts(true);
			var plugin = _imu.gameObject.AddComponent<ImuPlugin>();
			plugin.ChangePluginType(ICLOiSimPlugin.Type.REALSENSE);
			plugin.PartsName = _partsName;
			plugin.SubPartsName = name;

			AddPlugin(name, plugin);
			_activatedModules.Add(new Tuple<string, string>("imu", name));
		}
	}

	private CameraPlugin FindAndAddCameraPlugin(in string name)
	{
		foreach (var camera in _cameras)
		{
			if (camera.name.Equals(name))
			{
				camera.SetSubParts(true);
				var plugin = camera.gameObject.AddComponent<CameraPlugin>();
				plugin.ChangePluginType(ICLOiSimPlugin.Type.REALSENSE);
				plugin.PartsName = _partsName;
				plugin.SubPartsName = name;

				AddPlugin(name, plugin);
				_activatedModules.Add(new Tuple<string, string>("camera", name));
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
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
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

		foreach (var module in _activatedModules)
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