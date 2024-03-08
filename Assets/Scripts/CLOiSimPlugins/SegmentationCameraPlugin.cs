/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class SegmentationCameraPlugin : CameraPlugin
{
	protected SensorDevices.Camera cam = null;

	protected override void OnAwake()
	{
		var segCam = gameObject.GetComponent<SensorDevices.SegmentationCamera>();

		if (segCam is not null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.SEGMENTCAMERA);
			_cam = segCam;
			attachedDevices.Add("SegmentationCamera", _cam);
		}
		else
		{
			Debug.LogError("SensorDevices.SegmentationCamera is missing.");
		}

		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnPluginLoad()
	{
		if (GetPluginParameters() != null && type == ICLOiSimPlugin.Type.SEGMENTCAMERA)
		{
			if (GetPluginParameters().GetValues<string>("segmentation/label", out var labelList))
			{
				Main.SegmentationManager.SetClassFilter(labelList);
			}
			Main.SegmentationManager.UpdateTags();
		}
	}
}