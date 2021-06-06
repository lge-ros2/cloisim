/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class CameraPlugin : CLOiSimPlugin
{
	public string subPartName = string.Empty;

	public SensorDevices.Camera GetCamera()
	{
		return targetDevice as SensorDevices.Camera;
	}

	protected override void OnAwake()
	{
		var depthcam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		if (depthcam is null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
			targetDevice = gameObject.GetComponent<SensorDevices.Camera>();
		}
		else
		{
			ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
			targetDevice = depthcam;
		}

		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice(subPartName + "Info");
		RegisterTxDevice(subPartName + "Data");

		AddThread(ServiceThread);
		AddThread(SenderThread, targetDevice);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_camera_info":
				var cam = targetDevice as SensorDevices.Camera;
				var cameraInfoMessage = cam.GetCameraInfo();
				SetCameraInfoResponse(ref response, cameraInfoMessage);
				break;

			case "request_transform":
				var devicePose = targetDevice.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}