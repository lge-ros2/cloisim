/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class GpsPlugin : CLOiSimPlugin
{
	private SensorDevices.GPS _gps = null;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.GPS;
		_gps = gameObject.GetComponent<SensorDevices.GPS>();
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _gps);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _gps.GetPose();
				var deviceName = _gps.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}