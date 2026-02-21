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

public class GpsPlugin : CLOiSimPlugin
{
	private SensorDevices.GPS _gps = null;
	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosGpsPublisher = IntPtr.Zero;
	private IntPtr _rosImuPublisher = IntPtr.Zero;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.GPS;
		_gps = gameObject.GetComponent<SensorDevices.GPS>();
	}

	protected override IEnumerator OnStart()
	{
		Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_gps_" + gameObject.name.Replace(" ", "_");
		_rosNode = Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/gps");
		_rosGpsPublisher = Ros2NativeWrapper.CreateNavSatFixPublisher(_rosNode, topicName);
		
		var imuTopicName = GetPluginParameters().GetValue<string>("topic_heading", "/gps_heading");
		_rosImuPublisher = Ros2NativeWrapper.CreateImuPublisher(_rosNode, imuTopicName);

		_gps.OnGpsDataGenerated += HandleNativeGpsData;
		_gps.OnGpsHeadingGenerated += HandleNativeGpsHeading;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _gps);
		}
		yield return null;
	}

	private void HandleNativeGpsData(messages.Gps msg)
	{
		if (_rosGpsPublisher == IntPtr.Zero) return;

		var data = new NavSatFixStruct
		{
			timestamp = msg.Time.Sec + (msg.Time.Nsec * 1e-9),
			frame_id = msg.LinkName,
			status = 0, // STATUS_FIX
			service = 1, // SERVICE_GPS
			latitude = msg.LatitudeDeg,
			longitude = msg.LongitudeDeg,
			altitude = msg.Altitude,
			position_covariance = new double[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
			position_covariance_type = 0 // COVARIANCE_TYPE_UNKNOWN
		};

		Ros2NativeWrapper.PublishNavSatFix(_rosGpsPublisher, ref data);
	}

	private void HandleNativeGpsHeading(messages.Imu msg)
	{
		if (_rosImuPublisher == IntPtr.Zero) return;

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

		Ros2NativeWrapper.PublishImu(_rosImuPublisher, ref data);
	}

	protected void OnDestroy()
	{
		if (_gps != null) 
		{
			_gps.OnGpsDataGenerated -= HandleNativeGpsData;
			_gps.OnGpsHeadingGenerated -= HandleNativeGpsHeading;
		}

		if (_rosGpsPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyNavSatFixPublisher(_rosGpsPublisher);
		if (_rosImuPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyImuPublisher(_rosImuPublisher);
		if (_rosNode != IntPtr.Zero) Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _gps.GetPose();
				var deviceName = _gps.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}