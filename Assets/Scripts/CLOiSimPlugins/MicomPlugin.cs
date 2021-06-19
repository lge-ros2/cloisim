/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> tfList = new List<TF>();
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
		SetupMicom();

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, micomCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, micomSensor);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, tfList);
		}

		SetupMicom();
		LoadTFs();
	}

	private void SetupMicom()
	{
		micomSensor.EnableDebugging = GetPluginParameters().GetValue<bool>("debug", false);

		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		micomSensor.SetUpdateRate(updateRate);

		var wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");
		var wheelTread = GetPluginParameters().GetValue<float>("wheel/tread");
		var P = GetPluginParameters().GetValue<float>("wheel/PID/kp");
		var I = GetPluginParameters().GetValue<float>("wheel/PID/ki");
		var D = GetPluginParameters().GetValue<float>("wheel/PID/kd");

		micomSensor.SetMotorConfiguration(wheelRadius, wheelTread, P, I, D);

		var wheelNameLeft = GetPluginParameters().GetValue<string>("wheel/location[@type='left']");
		var wheelNameRight = GetPluginParameters().GetValue<string>("wheel/location[@type='right']");

		// TODO: to be utilized
		var motorFriction = GetPluginParameters().GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = GetPluginParameters().GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		micomSensor.SetWheel(wheelNameLeft, wheelNameRight);

		if (GetPluginParameters().GetValues<string>("uss/sensor", out var ussList))
		{
			micomSensor.SetUSS(ussList);
		}

		if (GetPluginParameters().GetValues<string>("ir/sensor", out var irList))
		{
			micomSensor.SetIRSensor(irList);
		}

		if (GetPluginParameters().GetValues<string>("magnet/sensor", out var magnetList))
		{
			micomSensor.SetMagnet(magnetList);
		}

		var targetContactName = GetPluginParameters().GetAttribute<string>("bumper", "contact");
		micomSensor.SetBumper(targetContactName);

		if (GetPluginParameters().GetValues<string>("bumper/joint_name", out var bumperJointNameList))
		{
			micomSensor.SetBumperSensor(bumperJointNameList);
		}
	}

	private void LoadTFs()
	{
		// var tf = new TF(targetLink, linkName, parentFrameId);
		// tfList.Add(tf);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			// case "request_transform_name":
			// 	SetTransformNameResponse(ref response);
			// 	break;

			case "request_transform":
				var devicePartsName = requestValue.StringValue;
				var devicePose = this.micomSensor.GetSubPartsPose(devicePartsName);
				SetTransformInfoResponse(ref response, devicePartsName, devicePose);

				break;

			case "reset_odometry":
				Reset();
				SetEmptyResponse(ref response);
				break;

			default:
				break;
		}
	}

	// private void SetTransformNameResponse(ref DeviceMessage msRos2Info)
	// {
	// 	if (msRos2Info == null)
	// 	{
	// 		return;
	// 	}

	// 	var ros2CommonInfo = new messages.Param();
	// 	ros2CommonInfo.Name = "ros2";
	// 	ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

	// 	var ros2TransformInfo = new messages.Param();
	// 	ros2TransformInfo.Name = "transform_name";
	// 	ros2TransformInfo.Value = new Any { Type = Any.ValueType.None };
	// 	ros2CommonInfo.Childrens.Add(ros2TransformInfo);

	// 	var imu_name = GetPluginParameters().GetValue<string>("ros2/transform_name/imu");
	// 	var imuInfo = new messages.Param();
	// 	imuInfo.Name = "imu";
	// 	imuInfo.Value = new Any { Type = Any.ValueType.String, StringValue = imu_name };
	// 	ros2TransformInfo.Childrens.Add(imuInfo);

	// 	var wheelsInfo = new messages.Param();
	// 	wheelsInfo.Name = "wheels";
	// 	wheelsInfo.Value = new Any { Type = Any.ValueType.None };
	// 	ros2TransformInfo.Childrens.Add(wheelsInfo);

	// 	var wheel_left_name = GetPluginParameters().GetValue<string>("ros2/transform_name/wheels/left");
	// 	var wheelLeftInfo = new messages.Param();
	// 	wheelLeftInfo.Name = "left";
	// 	wheelLeftInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_left_name };
	// 	wheelsInfo.Childrens.Add(wheelLeftInfo);

	// 	var wheel_right_name = GetPluginParameters().GetValue<string>("ros2/transform_name/wheels/right");
	// 	var wheelRightInfo = new messages.Param();
	// 	wheelRightInfo.Name = "right";
	// 	wheelRightInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_right_name };
	// 	wheelsInfo.Childrens.Add(wheelRightInfo);

	// 	msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	// }
}