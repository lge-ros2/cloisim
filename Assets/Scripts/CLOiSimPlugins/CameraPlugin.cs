/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class CameraPlugin : CLOiSimPlugin
{
	protected SensorDevices.Camera _cam = null;

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

		if (!string.IsNullOrEmpty(deviceName))
		{
			attachedDevices.Add(deviceName, _cam);
		}
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _cam);
		}
	}

	protected override void OnPluginLoad()
	{
		if (GetPluginParameters() != null && type == ICLOiSimPlugin.Type.DEPTHCAMERA)
		{
			var depthScale = GetPluginParameters().GetValue<uint>("configuration/depth_scale", 1000);
			if (_cam != null)
			{
				((SensorDevices.DepthCamera)_cam).SetDepthScale(depthScale);
			}
		}
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
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}