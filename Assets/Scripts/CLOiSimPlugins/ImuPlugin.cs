/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using Any = cloisim.msgs.Any;

public class ImuPlugin : CLOiSimPlugin
{
	private SensorDevices.IMU _imu = null;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.IMU;
		_imu = gameObject.GetComponent<SensorDevices.IMU>();
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _imu);
		}

		yield return null;
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _imu.GetPose();
				var deviceName = _imu.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}