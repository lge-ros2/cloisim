/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class MicomPlugin : CLOiSimPlugin
{
	private SensorDevices.MicomSensor micomSensor = null;
	private SensorDevices.MicomCommand micomCommand = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MICOM;
		micomSensor = gameObject.AddComponent<SensorDevices.MicomSensor>();

		micomCommand = gameObject.AddComponent<SensorDevices.MicomCommand>();
		micomCommand.SetMotorControl(micomSensor.MotorControl);

		attachedDevices.Add("Sensor", micomSensor);
		attachedDevices.Add("Command", micomCommand);
	}

	protected override void OnStart()
	{
		micomSensor.SetupMicom();

		RegisterServiceDevice("Info");
		RegisterRxDevice("Rx");
		RegisterTxDevice("Tx");

		AddThread(ServiceThread);
		AddThread(ReceiverThread, micomCommand);
		AddThread(SenderThread, micomSensor);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform_name":
				SetTransformNameResponse(ref response);
				break;

			case "request_transform":
				var transformPartsName = requestValue.StringValue;
				var devicePose = this.micomSensor.GetSubPartsPose(transformPartsName);
				SetTransformInfoResponse(ref response, devicePose);
				break;

			case "reset_odometry":
				Reset();
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