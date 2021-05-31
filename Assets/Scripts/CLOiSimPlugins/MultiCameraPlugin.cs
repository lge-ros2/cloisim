/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class MultiCameraPlugin : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MULTICAMERA;
		partsName = DeviceHelper.GetPartName(gameObject);
		targetDevice = gameObject.GetComponent<SensorDevices.MultiCamera>();
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(SenderThread, targetDevice);
		AddThread(RequestThread);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response)
	{
		var cameraName = requestValue;
		var multicam = targetDevice as SensorDevices.MultiCamera;
		var camera = multicam.GetCamera(cameraName);

		if (camera == null)
		{
			UnityEngine.Debug.LogWarning("cannot find camera from multicamera: " + cameraName);
			return;
		}

		switch (requestType)
		{
			case "request_camera_info":
				var cameraInfoMessage = camera.GetCameraInfo();
				CameraPlugin.SetCameraInfoResponse(ref response, cameraInfoMessage);
				break;

			case "request_transform":
				var devicePose = camera.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;
			default:
				break;
		}
	}
}