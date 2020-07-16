/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class RealSensePlugin : DevicesPlugin
{
	protected override void OnAwake()
	{
	}

	protected override void OnStart()
	{
		var rgbName = parameters.GetValue<string>("activate/module[@name='rgb']");
		var leftImagerName = parameters.GetValue<string>("activate/module[@name='left_imager']");
		var rightImagerName = parameters.GetValue<string>("activate/module[@name='right_imager']");
		var depthName = parameters.GetValue<string>("activate/module[@name='depth']");

		if (rgbName != null)
		{
			var rgbPlugin = AddDevicePlugin(rgbName, "CameraPlugin") as CameraPlugin;
			rgbPlugin.subPartName = rgbName;
		}

		if (leftImagerName != null)
		{
			var leftImagerPlugin = AddDevicePlugin(leftImagerName, "CameraPlugin") as CameraPlugin;
			leftImagerPlugin.subPartName = leftImagerName;
		}

		if (rightImagerName != null)
		{
			var rightImagerPlugin = AddDevicePlugin(rightImagerName, "CameraPlugin") as CameraPlugin;
			rightImagerPlugin.subPartName = rightImagerName;
		}

		if (depthName != null)
		{
			var depthPlugin = AddDevicePlugin(depthName, "CameraPlugin") as CameraPlugin;
			depthPlugin.subPartName = depthName;
		}
	}
}