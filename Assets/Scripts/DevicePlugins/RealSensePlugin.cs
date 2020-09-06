/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class RealSensePlugin : DevicesPlugin
{
	private Camera[] cameras = null;

	protected override void OnAwake()
	{
		cameras = GetComponentsInChildren<Camera>();
	}

	protected override void OnStart()
	{
		var rgbName = parameters.GetValue<string>("activate/module[@name='rgb']");
		var leftImagerName = parameters.GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = parameters.GetValue<string>("activate/module[@name='right_imager']");
		var depthName = parameters.GetValue<string>("activate/module[@name='depth']");

		if (rgbName != null)
		{
			FindAndAddPlugin(rgbName);
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
	}

	void FindAndAddPlugin(in string name)
	{
		foreach (var camera in cameras)
		{
			if (camera.gameObject.name.Equals(name))
			{
				var plugin = camera.gameObject.AddComponent<CameraPlugin>();
				plugin.ChangePluginType(DevicePlugin.Type.REALSENSE);
				plugin.subPartName = name;

				AddDevicePlugin(name, plugin);
				break;
			}
		}
	}
}