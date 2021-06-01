/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class GpsPlugin : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.GPS;
		targetDevice = gameObject.GetComponent<SensorDevices.GPS>();
		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(ServiceThread);
		AddThread(SenderThread, targetDevice);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = targetDevice.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}