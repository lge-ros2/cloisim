/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class CameraPlugin : CLOiSimPlugin
{
	private SensorDevices.Camera cam = null;
	private string subPartsName = string.Empty;

	public SensorDevices.Camera GetCamera()
	{
		return cam;
	}

	public SensorDevices.DepthCamera GetDepthCamera()
	{
		return GetCamera() as SensorDevices.DepthCamera;
	}

	public string SubPartsName
	{
		get => this.subPartsName;
		set => this.subPartsName = value;
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
		RegisterServiceDevice(subPartsName + "Info");
		RegisterTxDevice(subPartsName + "Data");

		AddThread(ServiceThread);
		AddThread(SenderThread, cam);
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
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}