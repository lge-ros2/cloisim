/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Any = cloisim.msgs.Any;

public class RangePlugin : CLOiSimPlugin
{
	protected enum RadiationType {
		ULTRASOUND=0,
		INFRARED
	}

	private SensorDevices.Sonar _sonar = null;

	[field: SerializeField]
	protected RadiationType _radiationType = RadiationType.ULTRASOUND;

	protected override void OnAwake()
	{
		_type = (_radiationType == RadiationType.ULTRASOUND) ? ICLOiSimPlugin.Type.SONAR : ICLOiSimPlugin.Type.IR;

		_sonar = gameObject.GetComponent<SensorDevices.Sonar>();

		_attachedDevices.Add(_sonar);
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