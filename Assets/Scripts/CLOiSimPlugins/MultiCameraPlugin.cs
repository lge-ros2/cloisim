/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class MultiCameraPlugin : CLOiSimPlugin
{
	private SensorDevices.MultiCamera multiCam = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MULTICAMERA;
		partsName = DeviceHelper.GetPartName(gameObject);

		multiCam = gameObject.GetComponent<SensorDevices.MultiCamera>();

		attachedDevices.Add("MultiCamera", multiCam);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(SenderThread, multiCam);
		AddThread(ServiceThread);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		var cameraName = requestValue.StringValue;
		var camera = multiCam.GetCamera(cameraName);

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
				var deviceName = camera.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;
			default:
				break;
		}
	}
}