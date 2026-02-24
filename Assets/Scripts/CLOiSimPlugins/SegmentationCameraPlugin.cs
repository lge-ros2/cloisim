/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using cloisim.Native;
using System.Runtime.InteropServices;
using System;
using messages = cloisim.msgs;

public class SegmentationCameraPlugin : CameraPlugin
{
	private IntPtr _rosLabelInfoPublisher = IntPtr.Zero;

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

		var segCam = _cam as SensorDevices.SegmentationCamera;
		if (segCam != null)
		{
			// The base class CameraPlugin has already initialized ROS2 and created the node for this device name.
			// However, since we need the ROS node pointer, we'll re-create or fetch the node.
			// To avoid modifying CameraPlugin to make _rosNode protected, we'll create a new node specifically for labels,
			// or we can just make _rosNode protected in CameraPlugin. For now, since it's private in CameraPlugin, Let's create a new node.
			var nodeName = "cloisim_segcam_labels_" + gameObject.name.Replace(" ", "_");
			var localRosNode = Ros2NativeWrapper.CreateNode(nodeName);

			var labelTopicName = GetPluginParameters().GetValue<string>("topic_label", "/segmentation_labels");
			_rosLabelInfoPublisher = Ros2NativeWrapper.CreateLabelInfoPublisher(localRosNode, labelTopicName);

			segCam.OnSegmentationDataGenerated += HandleNativeSegmentationData;

			yield return null;
		}

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

	private unsafe void HandleNativeSegmentationData(messages.Segmentation msg)
	{
		if (_rosLabelInfoPublisher == IntPtr.Zero) return;

		var numLabels = msg.ClassMaps.Count;
		var classIds = new int[numLabels];
		var classNames = new string[numLabels];
		
		for (int i = 0; i < numLabels; i++)
		{
			classIds[i] = (int)msg.ClassMaps[i].ClassId;
			classNames[i] = msg.ClassMaps[i].ClassName;
		}

		// Prepare unmanaged memory for arrays
		IntPtr classIdsPtr = Marshal.AllocHGlobal(numLabels * sizeof(int));
		Marshal.Copy(classIds, 0, classIdsPtr, numLabels);

		IntPtr classNamesPtr = Marshal.AllocHGlobal(numLabels * IntPtr.Size);
		IntPtr[] stringPointers = new IntPtr[numLabels];
		for (int i = 0; i < numLabels; i++)
		{
			stringPointers[i] = Marshal.StringToHGlobalAnsi(classNames[i]);
		}
		Marshal.Copy(stringPointers, 0, classNamesPtr, numLabels);

		try
		{
			var data = new LabelInfoStruct
			{
				timestamp = msg.ImageStamped.Time.Sec + (msg.ImageStamped.Time.Nsec * 1e-9),
				frame_id = _partsName,
				class_id = classIdsPtr,
				class_name = classNamesPtr,
				label_length = numLabels
			};

			Ros2NativeWrapper.PublishLabelInfo(_rosLabelInfoPublisher, ref data);
		}
		finally
		{
			// Free unmanaged memory
			Marshal.FreeHGlobal(classIdsPtr);
			for (int i = 0; i < numLabels; i++)
			{
				Marshal.FreeHGlobal(stringPointers[i]);
			}
			Marshal.FreeHGlobal(classNamesPtr);
		}
	}

	protected new void OnDestroy()
	{
		if (_cam != null)
		{
			var segCam = _cam as SensorDevices.SegmentationCamera;
			if (segCam != null) segCam.OnSegmentationDataGenerated -= HandleNativeSegmentationData;
		}

		if (_rosLabelInfoPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyLabelInfoPublisher(_rosLabelInfoPublisher);
		
		base.OnDestroy();
	}
}