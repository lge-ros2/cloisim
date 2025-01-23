/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// using UnityEngine;
using Any = cloisim.msgs.Any;

public class SonarPlugin : CLOiSimPlugin
{
	private SensorDevices.Sonar _sonar = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.SONAR;

		_sonar = gameObject.GetComponent<SensorDevices.Sonar>();
		attachedDevices.Add("SONAR", _sonar);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _sonar);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _sonar.GetPose();
				var deviceName = _sonar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}