/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class MicomPlugin : CLOiSimPlugin
{
	private MicomInput micomInput = null;
	private MicomSensor micomSensor = null;

	protected override void OnAwake()
	{
		type = Type.MICOM;
		micomSensor = gameObject.AddComponent<MicomSensor>();
		micomInput = gameObject.AddComponent<MicomInput>();
		micomInput.SetMicomSensor(micomSensor);
	}

	protected override void OnStart()
	{
		micomSensor.SetPluginParameters(GetPluginParameters());

		var debugging = GetPluginParameters().GetValue<bool>("debug", false);
		micomInput.EnableDebugging = debugging;

		RegisterServiceDevice("Info");
		RegisterRxDevice("Rx");
		RegisterTxDevice("Tx");

		AddThread(Response);
		AddThread(Receiver);
		AddThread(SenderThread, micomSensor as System.Object);
	}

	protected override void OnReset()
	{
		micomSensor.Reset();
		micomInput.Reset();
	}

	private void Receiver()
	{
		while (IsRunningThread && micomInput != null)
		{
			var receivedData = Subscribe();
			micomInput.PushDeviceMessage(receivedData);

			WaitThread();
		}
	}

	private void Response()
	{
		var dmInfoResponse = new DeviceMessage();

		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();
			var requestMessage = ParsingRequestMessage(receivedBuffer);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_ros2":
						SetROS2TransformInfoResponse(ref dmInfoResponse);
						break;

					case "request_wheel_info":
						SetWheelInfoResponse(ref dmInfoResponse);
						break;

					case "request_transform":
						var targetPartsName = (requestMessage.Value == null) ? string.Empty : requestMessage.Value.StringValue;
						var devicePose = micomSensor.GetPartsPose(targetPartsName);
						SetTransformInfoResponse(ref dmInfoResponse, devicePose);
						break;

					case "reset_odometry":
						micomSensor.Reset();
						SetEmptyResponse(ref dmInfoResponse);
						break;

					default:
						break;
				}

				SendResponse(dmInfoResponse);
			}

			WaitThread();
		}
	}

	private void SetROS2TransformInfoResponse(ref DeviceMessage msRos2Info)
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

	private void SetWheelInfoResponse(ref DeviceMessage msWheelInfo)
	{
		if (msWheelInfo == null)
		{
			return;
		}

		var wheelInfo = new messages.Param();
		wheelInfo.Name = "wheelInfo";
		wheelInfo.Value = new Any { Type = Any.ValueType.None };

		var baseInfo = new messages.Param();
		baseInfo.Name = "tread";
		baseInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = micomSensor.WheelBase };
		wheelInfo.Childrens.Add(baseInfo);

		var sizeInfo = new messages.Param();
		sizeInfo.Name = "radius";
		sizeInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = micomSensor.WheelRadius };
		wheelInfo.Childrens.Add(sizeInfo);

		msWheelInfo.SetMessage<messages.Param>(wheelInfo);
	}
}