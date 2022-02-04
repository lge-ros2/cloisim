/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class ImuPlugin : CLOiSimPlugin
{
	private SensorDevices.IMU imu = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.IMU;
		imu = gameObject.GetComponent<SensorDevices.IMU>();
		attachedDevices.Add("IMU", imu);
		partsName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, imu);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = imu.GetPose();
				var deviceName = imu.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}