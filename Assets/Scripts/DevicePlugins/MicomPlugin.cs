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

public class MicomPlugin : DevicePlugin
{
	private MicomInput micomInput = null;
	private MicomSensor micomSensor = null;

	protected override void OnAwake()
	{
		type = Type.MICOM;

		micomSensor = gameObject.AddComponent<MicomSensor>();
		micomSensor.SetPluginParameter(parameters);
		micomInput = gameObject.AddComponent<MicomInput>();
		micomInput.SetMicomSensor(micomSensor);
	}

	protected override void OnStart()
	{
		var debugging = parameters.GetValue<bool>("debug", false);
		micomInput.EnableDebugging = debugging;

		RegisterServiceDevice("Info");
		RegisterTxDevice("Tx");
		RegisterRxDevice("Rx");

		AddThread(Receiver);
		AddThread(Sender);
		AddThread(Response);
	}

	protected override void OnReset()
	{
		micomSensor.Reset();
		micomInput.Reset();
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (micomSensor != null)
			{
				var datastreamToSend = micomSensor.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				micomSensor.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
		}
	}

	private void Receiver()
	{
		while (IsRunningThread)
		{
			if (micomInput != null)
			{
				var receivedData = Subscribe();
				micomInput.SetDataStream(receivedData);
			}

			ThreadWait();
		}
	}

	private void Response()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_ros2":
						SetROS2TransformInfoResponse(ref msForInfoResponse);
						break;

					case "request_wheel_info":
						SetWheelInfoResponse(ref msForInfoResponse);
						break;

					case "request_transform":
						var targetPartsName = (requestMessage.Value == null) ? string.Empty : requestMessage.Value.StringValue;
						var devicePose = micomSensor.GetPartsPose(targetPartsName);
						SetTransformInfoResponse(ref msForInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}

			ThreadWait();
		}
	}

	private void SetROS2TransformInfoResponse(ref MemoryStream msRos2Info)
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

		var imu_name = parameters.GetValue<string>("ros2/transform_name/imu");
		var imuInfo = new messages.Param();
		imuInfo.Name = "imu";
		imuInfo.Value = new Any { Type = Any.ValueType.String, StringValue = imu_name };
		ros2TransformInfo.Childrens.Add(imuInfo);

		var wheelsInfo = new messages.Param();
		wheelsInfo.Name = "wheels";
		wheelsInfo.Value = new Any { Type = Any.ValueType.None };
		ros2TransformInfo.Childrens.Add(wheelsInfo);

		var wheel_left_name = parameters.GetValue<string>("ros2/transform_name/wheels/left");
		var wheelLeftInfo = new messages.Param();
		wheelLeftInfo.Name = "left";
		wheelLeftInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_left_name };
		wheelsInfo.Childrens.Add(wheelLeftInfo);

		var wheel_right_name = parameters.GetValue<string>("ros2/transform_name/wheels/right");
		var wheelRightInfo = new messages.Param();
		wheelRightInfo.Name = "right";
		wheelRightInfo.Value = new Any { Type = Any.ValueType.String, StringValue = wheel_right_name };
		wheelsInfo.Childrens.Add(wheelRightInfo);

		ClearMemoryStream(ref msRos2Info);
		Serializer.Serialize<messages.Param>(msRos2Info, ros2CommonInfo);
	}

	private void SetWheelInfoResponse(ref MemoryStream msWheelInfo)
	{
		if (msWheelInfo == null)
		{
			return;
		}

		var wheelInfo = new messages.Param();
		wheelInfo.Name = "wheelInfo";
		wheelInfo.Value = new Any { Type = Any.ValueType.None };

		var baseInfo = new messages.Param();
		baseInfo.Name = "base";
		baseInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = micomSensor.WheelBase };
		wheelInfo.Childrens.Add(baseInfo);

		var sizeInfo = new messages.Param();
		sizeInfo.Name = "radius";
		sizeInfo.Value = new Any { Type = Any.ValueType.Double, DoubleValue = micomSensor.WheelRadius };
		wheelInfo.Childrens.Add(sizeInfo);

		ClearMemoryStream(ref msWheelInfo);
		Serializer.Serialize<messages.Param>(msWheelInfo, wheelInfo);
	}
}