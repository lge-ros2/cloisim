/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class LaserPlugin : CLOiSimPlugin
{
	private SensorDevices.Lidar lidar = null;

	private string hashServiceKey = string.Empty;
	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.LASER;
		partName = DeviceHelper.GetPartName(gameObject);

		lidar = gameObject.GetComponent<SensorDevices.Lidar>();
		lidar.SetPluginParameters(GetPluginParameters());

		UnityEngine.Debug.Log(lidar.GetPluginParameters());
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(RequestThread);
		AddThread(SenderThread, lidar);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_output_type":
				SetOutputTypeResponse(ref response);
				break;

			case "request_transform":
				var devicePose = lidar.GetPose();
				SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}

	private void SetOutputTypeResponse(ref DeviceMessage msInfo)
	{
		var output_type = GetPluginParameters().GetValue<string>("output_type", "LaserScan");
		var outputTypeInfo = new messages.Param();
		outputTypeInfo.Name = "output_type";
		outputTypeInfo.Value = new Any { Type = Any.ValueType.String, StringValue = output_type };

		msInfo.SetMessage<messages.Param>(outputTypeInfo);
	}
}