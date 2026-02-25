/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
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
	}

	protected override IEnumerator OnStart()
	{
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

			if (GetPluginParameters().IsValidNode("tof/pattern"))
			{
				var tofPattern = GetPluginParameters().GetValue<string>("tof/pattern/uri");
				var fovMaskH = GetPluginParameters().GetValue<float>("tof/pattern/fov_mask/horizontal");
				var fovMaskV = GetPluginParameters().GetValue<float>("tof/pattern/fov_mask/vertical");
				((SensorDevices.DepthCamera)_cam).SetTofPattern(tofPattern, fovMaskH, fovMaskV);
			}

			if (GetPluginParameters().IsValidNode("tof/vertical_fov"))
			{
				var desiredVerticalFov = GetPluginParameters().GetValue<float>("tof/vertical_fov");
				((SensorDevices.DepthCamera)_cam).SetTofVerticalFov(desiredVerticalFov);
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
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}