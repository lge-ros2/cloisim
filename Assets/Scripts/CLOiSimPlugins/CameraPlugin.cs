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

public class CameraPlugin : CLOiSimPlugin
{
	protected SensorDevices.Camera _cam = null;
	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosImagePublisher = IntPtr.Zero;
	private IntPtr _rosCameraInfoPublisher = IntPtr.Zero;

	protected override void OnAwake()
	{
		var depthCam = gameObject.GetComponent<SensorDevices.DepthCamera>();

		var deviceName = string.Empty;
		if (depthCam is not null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
			deviceName = "DepthCamera";
			_cam = depthCam;
		}
		else
		{
			ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
			deviceName = "Camera";
			_cam = gameObject.GetComponent<SensorDevices.Camera>();
		}
	}

	protected override IEnumerator OnStart()
	{
		Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_camera_" + gameObject.name.Replace(" ", "_");
		_rosNode = Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/image_raw");
		_rosImagePublisher = Ros2NativeWrapper.CreateImagePublisher(_rosNode, topicName);
		
		var infoTopicName = GetPluginParameters().GetValue<string>("topic_info", "/camera_info");
		_rosCameraInfoPublisher = Ros2NativeWrapper.CreateCameraInfoPublisher(_rosNode, infoTopicName);

		_cam.OnCameraDataGenerated += HandleNativeCameraData;
		_cam.OnCameraInfoGenerated += HandleNativeCameraInfo;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _cam);
		}

		yield return null;
	}

	protected override void OnPluginLoad()
	{
		if (GetPluginParameters() != null && _type == ICLOiSimPlugin.Type.DEPTHCAMERA)
		{
			var depthScale = GetPluginParameters().GetValue<uint>("configuration/depth_scale", 1000);
			if (_cam != null)
			{
				((SensorDevices.DepthCamera)_cam).SetDepthScale(depthScale);
			}
		}
	}

	private unsafe void HandleNativeCameraData(messages.ImageStamped msg)
	{
		if (_rosImagePublisher == IntPtr.Zero) return;

		fixed (byte* dataPtr = msg.Image.Data)
		{
			var data = new ImageStruct
			{
				timestamp = msg.Time.Sec + (msg.Time.Nsec * 1e-9),
				frame_id = _partsName,
				height = msg.Image.Height,
				width = msg.Image.Width,
				encoding = msg.Image.PixelFormat switch {
					1 => "l8", // L_INT8
					2 => "l16", // L_INT16
					3 => "rgb8", // RGB_INT8
					4 => "rgba8", // RGBA_INT8
					5 => "bgra8", // BGRA_INT8
					6 => "bgr8", // BGR_INT8
					_ => "rgb8"
				},
				is_bigendian = 0,
				step = msg.Image.Step,
				data = (IntPtr)dataPtr,
				data_length = (uint)msg.Image.Data.Length
			};

			Ros2NativeWrapper.PublishImage(_rosImagePublisher, ref data);
		}
	}

	private unsafe void HandleNativeCameraInfo(messages.CameraSensor msg)
	{
		if (_rosCameraInfoPublisher == IntPtr.Zero) return;

		var data = new CameraInfoStruct
		{
			timestamp = DeviceHelper.GlobalClock.SimTime, 
			frame_id = _partsName,
			height = (uint)msg.ImageSize.Y,
			width = (uint)msg.ImageSize.X,
			distortion_model = "plumb_bob",
			d = IntPtr.Zero,
			d_length = 5,
			k = new double[9] { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
			r = new double[9] { 1, 0, 0, 0, 1, 0, 0, 0, 1 },
			p = new double[12] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
			binning_x = 0,
			binning_y = 0
		};

		if (msg.Distortion != null)
		{
			var dArray = new double[] { msg.Distortion.K1, msg.Distortion.K2, msg.Distortion.P1, msg.Distortion.P2, msg.Distortion.K3 };
			fixed (double* dPtr = dArray)
			{
				data.d = (IntPtr)dPtr;
				
				// Calculate camera intrinsics based on FOV
				double fx = data.width / (2.0 * Math.Tan(msg.HorizontalFov / 2.0));
				double fy = fx; // Assuming square pixels
				double cx = data.width / 2.0;
				double cy = data.height / 2.0;

				data.k[0] = fx; data.k[2] = cx;
				data.k[4] = fy; data.k[5] = cy;
				data.k[8] = 1.0;

				data.p[0] = fx; data.p[2] = cx;
				data.p[5] = fy; data.p[6] = cy;
				data.p[10] = 1.0;

				Ros2NativeWrapper.PublishCameraInfo(_rosCameraInfoPublisher, ref data);
			}
		}
		else
		{
			// Calculate camera intrinsics based on FOV
			double fx = data.width / (2.0 * Math.Tan(msg.HorizontalFov / 2.0));
			double fy = fx; 
			double cx = data.width / 2.0;
			double cy = data.height / 2.0;

			data.k[0] = fx; data.k[2] = cx;
			data.k[4] = fy; data.k[5] = cy;
			data.k[8] = 1.0;

			data.p[0] = fx; data.p[2] = cx;
			data.p[5] = fy; data.p[6] = cy;
			data.p[10] = 1.0;

			Ros2NativeWrapper.PublishCameraInfo(_rosCameraInfoPublisher, ref data);
		}
	}

	protected void OnDestroy()
	{
		if (_cam != null)
		{
			_cam.OnCameraDataGenerated -= HandleNativeCameraData;
			_cam.OnCameraInfoGenerated -= HandleNativeCameraInfo;
		}

		if (_rosImagePublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyImagePublisher(_rosImagePublisher);
		if (_rosCameraInfoPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyCameraInfoPublisher(_rosCameraInfoPublisher);
		if (_rosNode != IntPtr.Zero) Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_camera_info":
				var cameraInfoMessage = _cam.GetCameraInfo();
				SetCameraInfoResponse(ref response, cameraInfoMessage);
				break;

			case "request_transform":
				var devicePose = _cam.GetPose();
				var deviceName = _cam.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}