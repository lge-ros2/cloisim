/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Stopwatch = System.Diagnostics.Stopwatch;

public class GpsPlugin : CLOiSimPlugin
{
	private SensorDevices.GPS gps = null;

	private string hashServiceKey = string.Empty;
	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.GPS;

		gps = gameObject.GetComponent<SensorDevices.GPS>();

		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(RequestThread);
		AddThread(SenderThread, gps);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var device = gps as Device;
				var devicePose = device.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}