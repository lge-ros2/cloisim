/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class GpsPlugin : CLOiSimPlugin
{
	private SensorDevices.GPS gps = null;
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.GPS;
		gps = gameObject.GetComponent<SensorDevices.GPS>();
		attachedDevices.Add("GPS", gps);
		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(ServiceThread);
		AddThread(SenderThread, gps);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = gps.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}