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
		_type = ICLOiSimPlugin.Type.GPS;

		gps = gameObject.GetComponent<SensorDevices.GPS>();
		_attachedDevices.Add(gps);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, gps);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = gps.GetPose();
				var deviceName = gps.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}