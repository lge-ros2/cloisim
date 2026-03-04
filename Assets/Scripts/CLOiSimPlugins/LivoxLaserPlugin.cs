/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class LivoxLaserPlugin : CLOiSimPlugin
{
	private SensorDevices.LivoxLidar _livoxLidar = null;
	private string _outputType = "LaserScan";

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.LASER;
		_livoxLidar = GetComponent<SensorDevices.LivoxLidar>();
	}

	protected override IEnumerator OnStart()
	{
		if (GetPluginParameters().IsValidNode("custom_noise"))
		{
			var customNoiseInRawXml = GetPluginParameters().GetValue<string>("custom_noise");
			_livoxLidar.SetupCustomNoise(customNoiseInRawXml);
		}

		_outputType = GetPluginParameters().GetValue<string>("output_type", "LaserScan");

		// Tell the device what output format the bridge expects
		_livoxLidar.OutputType = _outputType;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _livoxLidar);
		}

		yield return null;
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_output_type":
				SetOutputTypeResponse(ref response);
				break;

			case "request_transform":
				var devicePose = _livoxLidar.GetPose();
				var deviceName = _livoxLidar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}

	private void SetOutputTypeResponse(ref DeviceMessage msInfo)
	{
		var outputTypeInfo = new messages.Param();
		outputTypeInfo.Name = "output_type";
		outputTypeInfo.Value = new Any { Type = Any.ValueType.String, StringValue = _outputType };

		msInfo.SetMessage<messages.Param>(outputTypeInfo);
	}
}
