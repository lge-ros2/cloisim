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
		var depthCam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		var segCam = gameObject.GetComponent<SensorDevices.SegmentationCamera>();

		var deviceName = string.Empty;
		if (depthCam is null && segCam is not null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.SEGMENTCAMERA);
			cam = segCam;
			deviceName = "SegmentationCamera";
		}
		else if (depthCam is not null && segCam is null)
		{
			ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
			cam = depthCam;
			deviceName = "DepthCamera";
		}
		else
		{
			ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
			cam = gameObject.GetComponent<SensorDevices.Camera>();
			deviceName = "Camera";
		}

		if (!string.IsNullOrEmpty(deviceName))
			attachedDevices.Add(deviceName, cam);

		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		if (type == ICLOiSimPlugin.Type.DEPTHCAMERA)
		{
			if (GetPluginParameters() != null)
			{
				var depthScale = GetPluginParameters().GetValue<uint>("configuration/depth_scale", 1000);
				if (cam != null)
				{
					((SensorDevices.DepthCamera)cam).SetDepthScale(depthScale);
				}
			}
		}

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