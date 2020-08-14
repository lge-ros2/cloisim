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
	private MemoryStream memoryStreamForCameraInfo = null;

	public string subPartName = string.Empty;

	protected override void OnAwake()
	{
		memoryStreamForCameraInfo = new MemoryStream();
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

	void OnDestroy()
	{
		memoryStreamForCameraInfo.Dispose();
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

			var requestMessage = ParsingCameraInfoRequest(ref memoryStreamForCameraInfo, receivedBuffer);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null && requestMessage.Name.Equals("request_camera_info"))
			{
				var cameraInfoMessage = cam.GetCameraInfo();

				SetCameraInfoResponse(ref memoryStreamForCameraInfo, cameraInfoMessage);

				SendResponse(memoryStreamForCameraInfo);
			}
		}
	}

	public static messages.Param ParsingCameraInfoRequest(ref MemoryStream msCameraInfo, in byte[] receivedBuffer)
	{
		ClearMemoryStream(ref msCameraInfo);

		msCameraInfo.Write(receivedBuffer, 0, receivedBuffer.Length);
		msCameraInfo.Position = 0;

		return Serializer.Deserialize<messages.Param>(msCameraInfo);
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