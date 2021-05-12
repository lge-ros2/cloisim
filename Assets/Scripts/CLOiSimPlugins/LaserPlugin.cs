/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using ProtoBuf;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class LaserPlugin : CLOiSimPlugin
{
	private SensorDevices.Lidar lidar = null;

	private string hashServiceKey = string.Empty;
	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = Type.LASER;
		partName = DeviceHelper.GetPartName(gameObject);

		lidar = gameObject.GetComponent<SensorDevices.Lidar>();
		lidar.SetPluginParameter(parameters);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(Response);
		AddThread(SenderThread, lidar as System.Object);
	}

	private void Response()
	{
		var dmInfoResponse = new DeviceMessage();
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref dmInfoResponse);

			if (requestMessage != null)
			{
				var device = lidar as Device;

				switch (requestMessage.Name)
				{
					case "request_ros2":
						var topic_name = parameters.GetValue<string>("ros2/topic_name");
						var frame_id = parameters.GetValue<string>("ros2/frame_id");
						SetROS2CommonInfoResponse(ref dmInfoResponse, topic_name, frame_id);
						break;

					case "request_output_type":
						SetOutputTypeResponse(ref dmInfoResponse);
						break;

					case "request_transform":
						var devicePose = device.GetPose();

						SetTransformInfoResponse(ref dmInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(dmInfoResponse);
			}

			WaitThread();
		}
	}

	private void SetOutputTypeResponse(ref DeviceMessage msInfo)
	{
		var output_type = parameters.GetValue<string>("output_type", "LaserScan");
		var outputTypeInfo = new messages.Param();
		outputTypeInfo.Name = "output_type";
		outputTypeInfo.Value = new Any { Type = Any.ValueType.String, StringValue = output_type };

		msInfo.SetMessage<messages.Param>(outputTypeInfo);
	}
}