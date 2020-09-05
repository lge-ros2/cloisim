/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using ProtoBuf;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

public class MicomPlugin : DevicePlugin
{
	private MicomInput micomInput = null;
	private MicomSensor micomSensor = null;

	private string hashServiceKey;
	private string hashTxKey;
	private string hashRxKey;

	protected override void OnAwake()
	{
		partName = "MICOM";

		micomSensor = gameObject.AddComponent<MicomSensor>();
		micomSensor.SetPluginParameter(parameters);
		micomInput = gameObject.AddComponent<MicomInput>();
		micomInput.SetMicomSensor(micomSensor);
	}

	protected override void OnStart()
	{
		var debugging = parameters.GetValue<bool>("debug", false);
		micomInput.EnableDebugging = debugging;

		hashServiceKey = MakeHashKey("_SENSOR" + "Info");
		if (!RegisterServiceDevice(hashServiceKey))
		{
			Debug.LogError("Failed to register service - " + hashServiceKey);
		}

		hashTxKey = MakeHashKey("_SENSOR");
		if (!RegisterTxDevice(hashTxKey))
		{
			Debug.LogError("Failed to register for MicomPlugin TX- " + hashTxKey);
		}

		hashRxKey = MakeHashKey("_INPUT");
		if (!RegisterRxDevice(hashRxKey))
		{
			Debug.LogError("Failed to register for MicomPlugin RX- " + hashRxKey);
		}

		AddThread(Receiver);
		AddThread(Sender);
		AddThread(Response);
	}

	protected override void OnTerminate()
	{
		DeregisterDevice(hashRxKey);
		DeregisterDevice(hashTxKey);
		DeregisterDevice(hashServiceKey);
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
					case "request_wheel_info":
						SetWheelInfoResponse(ref msForInfoResponse);
						break;

					case "request_transform":
						var targetPartsName = requestMessage.Value.StringValue;
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

	private void SetWheelInfoResponse(ref MemoryStream msCameraInfo)
	{
		if (msCameraInfo != null)
		{
			var wheelInfo = new messages.Param();
			wheelInfo.Name = "wheelInfo";
			wheelInfo.Value = new messages.Any();
			wheelInfo.Value.Type = messages.Any.ValueType.None;

			var baseInfo = new messages.Param();
			baseInfo.Name = "base";
			baseInfo.Value = new messages.Any();
			baseInfo.Value.Type = messages.Any.ValueType.Double;
			baseInfo.Value.DoubleValue = micomSensor.WheelBase;
			wheelInfo.Childrens.Add(baseInfo);

			var sizeInfo = new messages.Param();
			sizeInfo.Name = "radius";
			sizeInfo.Value = new messages.Any();
			sizeInfo.Value.Type = messages.Any.ValueType.Double;
			sizeInfo.Value.DoubleValue = micomSensor.WheelRadius;
			wheelInfo.Childrens.Add(sizeInfo);

			ClearMemoryStream(ref msCameraInfo);
			Serializer.Serialize<messages.Param>(msCameraInfo, wheelInfo);
		}
	}
}