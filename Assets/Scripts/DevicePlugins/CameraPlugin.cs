/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using ProtoBuf;
using messages = gazebo.msgs;

public class CameraPlugin : DevicePlugin
{
	private SensorDevices.Camera cam = null;

	public string subPartName = string.Empty;

	protected override void OnAwake()
	{
		cam = gameObject.GetComponent<SensorDevices.Camera>();
		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var hashServiceKey = MakeHashKey(partName, subPartName + "Info");
		if (!RegisterServiceDevice(hashServiceKey))
		{
			Debug.LogError("Failed to register ElevatorSystem service - " + hashServiceKey);
		}

		var hashTxKey = MakeHashKey(partName, subPartName);
		if (!RegisterTxDevice(hashTxKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashTxKey);
		}

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		Stopwatch sw = new Stopwatch();
		while (true)
		{
			if (cam == null)
			{
				continue;
			}

			var datastreamToSend = cam.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			cam.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}

	private void Response()
	{
		while (true)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_camera_info":

						var cameraInfoMessage = cam.GetCameraInfo();

						SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);

						break;

					case "request_transform":
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}
		}
	}

	public static messages.Param ParsingInfoRequest(in byte[] srcReceivedBuffer, ref MemoryStream dstCameraInfoMemStream)
	{
		ClearMemoryStream(ref dstCameraInfoMemStream);

		dstCameraInfoMemStream.Write(srcReceivedBuffer, 0, srcReceivedBuffer.Length);
		dstCameraInfoMemStream.Position = 0;

		return Serializer.Deserialize<messages.Param>(dstCameraInfoMemStream);
	}

	public static void SetCameraInfoResponse(ref MemoryStream msCameraInfo,in messages.CameraSensor sensorInfo)
	{
		if (sensorInfo != null)
		{
			ClearMemoryStream(ref msCameraInfo);

			Serializer.Serialize<messages.CameraSensor>(msCameraInfo, sensorInfo);
		}
	}
}