/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class CameraPlugin : CLOiSimPlugin
{
	private SensorDevices.Camera cam = null;

	public SensorDevices.Camera GetCamera()
	{
		return cam;
	}

	public SensorDevices.DepthCamera GetDepthCamera()
	{
		return GetCamera() as SensorDevices.DepthCamera;
	}

	protected override void OnAwake()
	{
		var depthcam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		if (depthcam is null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
			cam = gameObject.GetComponent<SensorDevices.Camera>();
			attachedDevices.Add("Camera", cam);
		}
		else
		{
			ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
			cam = depthcam;
			attachedDevices.Add("DepthCamera", cam);
		}

		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, cam);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_camera_info":
				var cameraInfoMessage = cam.GetCameraInfo();
				SetCameraInfoResponse(ref response, cameraInfoMessage);
				break;

			case "request_transform":
				var devicePose = cam.GetPose();
				var deviceName = cam.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}