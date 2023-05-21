/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// using UnityEngine;
using Any = cloisim.msgs.Any;

public class SonarPlugin : CLOiSimPlugin
{
	private SensorDevices.Sonar sonar = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.SONAR;
		sonar = gameObject.GetComponent<SensorDevices.Sonar>();
		attachedDevices.Add("SONAR", sonar);
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
			AddThread(portTx, SenderThread, sonar);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = sonar.GetPose();
				var deviceName = sonar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}