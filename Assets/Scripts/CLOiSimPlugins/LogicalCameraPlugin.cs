/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;

public class LogicalCameraPlugin : CLOiSimPlugin
{
	private SensorDevices.LogicalCamera _logicalCamera = null;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.LOGICALCAMERA;
		_logicalCamera = gameObject.GetComponent<SensorDevices.LogicalCamera>();
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _logicalCamera);
		}
		yield return null;
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _logicalCamera.GetPose();
				var deviceName = _logicalCamera.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			case "request_logical_camera":
				SetLogicalCameraSensorInfoResponse(ref response);
				break;

			default:
				break;
		}
	}

	private void SetLogicalCameraSensorInfoResponse(ref DeviceMessage response)
	{
		if (_logicalCamera == null || response == null)
			return;

		var sensorInfo = new messages.LogicalCameraSensor
		{
			NearClip = _logicalCamera.Near,
			FarClip = _logicalCamera.Far,
			HorizontalFov = _logicalCamera.HorizontalFov,
			AspectRatio = _logicalCamera.AspectRatio
		};

		response.SetMessage(sensorInfo);
	}
}
