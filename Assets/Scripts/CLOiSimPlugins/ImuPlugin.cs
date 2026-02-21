/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using cloisim.Native;
using System.Runtime.InteropServices;
using System;
using messages = cloisim.msgs;

public class ImuPlugin : CLOiSimPlugin
{
	private SensorDevices.IMU imu = null;
	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosPublisher = IntPtr.Zero;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.IMU;
		imu = gameObject.GetComponent<SensorDevices.IMU>();
	}

	protected override IEnumerator OnStart()
	{
		Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_imu_" + gameObject.name.Replace(" ", "_");
		_rosNode = Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/imu");
		_rosPublisher = Ros2NativeWrapper.CreateImuPublisher(_rosNode, topicName);
		
		imu.OnImuDataGenerated += HandleNativeImuData;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, imu);
		}

		yield return null;
	}

	private void HandleNativeImuData(messages.Imu msg)
	{
		if (_rosPublisher == IntPtr.Zero) return;

		var data = new ImuStruct
		{
			timestamp = msg.Stamp.Sec + (msg.Stamp.Nsec * 1e-9),
			frame_id = msg.EntityName,
			orientation_x = msg.Orientation.X,
			orientation_y = msg.Orientation.Y,
			orientation_z = msg.Orientation.Z,
			orientation_w = msg.Orientation.W,
			angular_velocity_x = msg.AngularVelocity.X,
			angular_velocity_y = msg.AngularVelocity.Y,
			angular_velocity_z = msg.AngularVelocity.Z,
			linear_acceleration_x = msg.LinearAcceleration.X,
			linear_acceleration_y = msg.LinearAcceleration.Y,
			linear_acceleration_z = msg.LinearAcceleration.Z
		};

		Ros2NativeWrapper.PublishImu(_rosPublisher, ref data);
	}

	protected void OnDestroy()
	{
		if (imu != null) imu.OnImuDataGenerated -= HandleNativeImuData;
		if (_rosPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyImuPublisher(_rosPublisher);
		if (_rosNode != IntPtr.Zero) Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = imu.GetPose();
				var deviceName = imu.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}