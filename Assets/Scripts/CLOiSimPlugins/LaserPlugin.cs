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

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.LASER;
		partsName = DeviceHelper.GetPartName(gameObject);

		lidar = GetComponent<SensorDevices.Lidar>();
		attachedDevices.Add("LIDAR", lidar);
	}

	protected override void OnStart()
	{
		if (GetPluginParameters().IsValidNode("filter"))
		{
			var useIntensity = GetPluginParameters().GetValue<bool>("intensity");

			if (GetPluginParameters().IsValidNode("filter/angle/horizontal"))
			{
				var filterAngleLower = GetPluginParameters().GetValue<double>("filter/angle/horizontal/lower", double.NegativeInfinity);
				var filterAngleUpper = GetPluginParameters().GetValue<double>("filter/angle/horizontal/upper", double.PositiveInfinity);
				lidar.SetupLaserAngleFilter(filterAngleLower, filterAngleUpper, useIntensity);
			}

			if (GetPluginParameters().IsValidNode("filter/range"))
			{
				var filterRangeMin = GetPluginParameters().GetValue<double>("filter/range/min", double.NegativeInfinity);
				var filterRangeMax = GetPluginParameters().GetValue<double>("filter/range/max", double.PositiveInfinity);
				lidar.SetupLaserRangeFilter(filterRangeMin, filterRangeMax, useIntensity);
			}
		}

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, lidar);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_output_type":
				SetOutputTypeResponse(ref response);
				break;

			case "request_transform":
				var devicePose = lidar.GetPose();
				var deviceName = lidar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose);
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