/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using UnityEngine;
using System;
using Any = cloisim.msgs.Any;

public class RangePlugin : CLOiSimPlugin
{
	protected enum RadiationType {
		ULTRASOUND=0,
		INFRARED
	}

	private SensorDevices.Sonar _sonar = null;

	[field: SerializeField]
	protected RadiationType _radiationType = RadiationType.ULTRASOUND;

	protected override void OnAwake()
	{
		_type = (_radiationType == RadiationType.ULTRASOUND) ? ICLOiSimPlugin.Type.SONAR : ICLOiSimPlugin.Type.IR;
		_sonar = gameObject.GetComponent<SensorDevices.Sonar>();
	}

	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosPublisher = IntPtr.Zero;
	private IntPtr _rosPosePublisher = IntPtr.Zero;

	protected override IEnumerator OnStart()
	{
		// Initialize ROS2 Native Plugin
		cloisim.Native.Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_range_" + gameObject.name.Replace(" ", "_");
		_rosNode = cloisim.Native.Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/range");
		_rosPublisher = cloisim.Native.Ros2NativeWrapper.CreateRangePublisher(_rosNode, topicName);
		
		var poseTopicName = GetPluginParameters().GetValue<string>("topic_pose", "/range_pose");
		_rosPosePublisher = cloisim.Native.Ros2NativeWrapper.CreatePoseStampedPublisher(_rosNode, poseTopicName);

		_sonar.OnRangeDataGenerated += HandleNativeRangeData;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _sonar);
		}
		yield return null;
	}

	private void HandleNativeRangeData(cloisim.msgs.SonarStamped sonarStamped)
	{
		double timestamp = sonarStamped.Time.Sec + (sonarStamped.Time.Nsec * 1e-9);

		if (_rosPublisher != IntPtr.Zero)
		{
			var rangeData = new cloisim.Native.RangeStruct
			{
				timestamp = timestamp,
				frame_id = _sonar.DeviceName,
				radiation_type = (byte)_radiationType,
				field_of_view = (float)_sonar.Radius,
				min_range = (float)_sonar.RangeMin,
				max_range = (float)_sonar.RangeMax,
				range = (float)sonarStamped.Sonar.Range
			};

			cloisim.Native.Ros2NativeWrapper.PublishRange(_rosPublisher, ref rangeData);
		}

		if (_rosPosePublisher != IntPtr.Zero)
		{
			var poseData = new cloisim.Native.PoseStampedStruct
			{
				timestamp = timestamp,
				frame_id = _sonar.DeviceName,
				position_x = sonarStamped.Sonar.WorldPose.Position.X,
				position_y = sonarStamped.Sonar.WorldPose.Position.Y,
				position_z = sonarStamped.Sonar.WorldPose.Position.Z,
				orientation_x = sonarStamped.Sonar.WorldPose.Orientation.X,
				orientation_y = sonarStamped.Sonar.WorldPose.Orientation.Y,
				orientation_z = sonarStamped.Sonar.WorldPose.Orientation.Z,
				orientation_w = sonarStamped.Sonar.WorldPose.Orientation.W
			};

			cloisim.Native.Ros2NativeWrapper.PublishPoseStamped(_rosPosePublisher, ref poseData);
		}
	}

	new protected void OnDestroy()
	{
		if (_sonar != null) _sonar.OnRangeDataGenerated -= HandleNativeRangeData;
		if (_rosPublisher != IntPtr.Zero) cloisim.Native.Ros2NativeWrapper.DestroyRangePublisher(_rosPublisher);
		if (_rosPosePublisher != IntPtr.Zero) cloisim.Native.Ros2NativeWrapper.DestroyPoseStampedPublisher(_rosPosePublisher);
		if (_rosNode != IntPtr.Zero) cloisim.Native.Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _sonar.GetPose();
				var deviceName = _sonar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}