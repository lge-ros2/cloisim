/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class MicomPlugin : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MICOM;
		targetDevice = gameObject.AddComponent<Micom>();
	}

	protected override void OnStart()
	{
		var debugging = GetPluginParameters().GetValue<bool>("debug", false);
		targetDevice.EnableDebugging = debugging;

		RegisterServiceDevice("Info");
		RegisterRxDevice("Rx");
		RegisterTxDevice("Tx");

		AddThread(ServiceThread);
		AddThread(ReceiverThread, (targetDevice as Micom).GetCommand());
		AddThread(SenderThread, (targetDevice as Micom).GetSensor());
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform_name":
				SetTransformNameResponse(ref response);
				break;

			case "request_transform":
				var micomSensor = (targetDevice as Micom).GetSensor();
				var transformPartsName = requestValue.StringValue;
				var devicePose = micomSensor.GetPartsPose(transformPartsName);
				SetTransformInfoResponse(ref response, devicePose);
				break;

			case "reset_odometry":
				targetDevice.Reset();
				SetEmptyResponse(ref response);
				break;

			default:
				break;
		}
	}

	private void SetTransformNameResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "ros2";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		var ros2TransformInfo = new messages.Param();
		ros2TransformInfo.Name = "transform_name";
		ros2TransformInfo.Value = new Any { Type = Any.ValueType.None };
		ros2CommonInfo.Childrens.Add(ros2TransformInfo);

		var imu_name = GetPluginParameters().GetValue<string>("ros2/transform_name/imu");
		var imuInfo = new messages.Param();
		imuInfo.Name = "imu";
		imuInfo.Value = new Any { Type = Any.ValueType.String, StringValue = imu_name };
		ros2TransformInfo.Childrens.Add(imuInfo);

		var wheelsInfo = new messages.Param();
		wheelsInfo.Name = "wheels";
		wheelsInfo.Value = new Any { Type = Any.ValueType.None };
		ros2TransformInfo.Childrens.Add(wheelsInfo);

		var wheel_left_name = GetPluginParameters().GetValue<string>("ros2/transform_name/wheels/left");
		var wheelLeftInfo = new messages.Param();
		wheelLeftInfo.Name = "left";
		wheelLeftInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_left_name };
		wheelsInfo.Childrens.Add(wheelLeftInfo);

		var wheel_right_name = GetPluginParameters().GetValue<string>("ros2/transform_name/wheels/right");
		var wheelRightInfo = new messages.Param();
		wheelRightInfo.Name = "right";
		wheelRightInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_right_name };
		wheelsInfo.Childrens.Add(wheelRightInfo);

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}