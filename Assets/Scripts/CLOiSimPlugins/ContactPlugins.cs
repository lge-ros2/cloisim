/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Any = cloisim.msgs.Any;

public class ContactPlugin : CLOiSimPlugin
{
	private SensorDevices.Contact _contact = null;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.CONTACT;

		_contact = gameObject.GetComponent<SensorDevices.Contact>();
		_attachedDevices.Add(_contact);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _contact);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _contact.GetPose();
				var deviceName = _contact.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}