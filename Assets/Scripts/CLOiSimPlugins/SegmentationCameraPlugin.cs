/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class SegmentationCameraPlugin : CameraPlugin
{
	protected override void OnAwake()
	{
		var segCam = gameObject.GetComponent<SensorDevices.SegmentationCamera>();

		if (segCam is not null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.SEGMENTCAMERA);
			_cam = segCam;
		}
		else
		{
			Debug.LogError("SensorDevices.SegmentationCamera is missing.");
		}
	}

	protected override IEnumerator OnStart()
	{
		yield return base.OnStart();

		// Apply segmentation class filter from plugin parameters.
		// Must be done here (not OnPluginLoad) because plugin parameters
		// are set after Awake() where OnPluginLoad() runs.
		if (GetPluginParameters() != null && _type == ICLOiSimPlugin.Type.SEGMENTCAMERA)
		{
			if (GetPluginParameters().GetValues<string>("segmentation/label", out var labelList))
			{
				Main.SegmentationManager.SetClassFilter(labelList);
			}
			Main.SegmentationManager.UpdateTags();
		}
	}

	protected override void OnPluginLoad()
	{
		// Segmentation label filter is now applied in OnStart() instead,
		// because OnPluginLoad() runs during Awake() before plugin parameters are set.
	}
}