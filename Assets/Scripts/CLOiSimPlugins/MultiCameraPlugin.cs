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
		_type = ICLOiSimPlugin.Type.MULTICAMERA;

		multiCam = gameObject.GetComponent<SensorDevices.MultiCamera>();
		_attachedDevices.Add(multiCam);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, multiCam);
		}
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
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}