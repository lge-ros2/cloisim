/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class CameraPlugin : CLOiSimPlugin
{
	private SensorDevices.Camera cam = null;

	public string subPartName = string.Empty;

	public SensorDevices.Camera GetCamera()
	{
		return cam;
	}

	protected override void OnAwake()
	{
		var depthcam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		if (depthcam is null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
			cam = gameObject.GetComponent<SensorDevices.Camera>();
		}
		else
		{
			ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
			cam = depthcam;
		}

		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice(subPartName + "Info");
		RegisterTxDevice(subPartName + "Data");

		AddThread(RequestThread);
		AddThread(SenderThread, cam);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_camera_info":
				var cameraInfoMessage = cam.GetCameraInfo();
				SetCameraInfoResponse(ref response, cameraInfoMessage);
				break;

			case "request_transform":
				var isSubParts = string.IsNullOrEmpty(subPartName);
				var devicePose = cam.GetPose(isSubParts);
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}