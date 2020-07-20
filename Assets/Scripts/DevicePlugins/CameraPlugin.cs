/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System.Text;
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

			var receivedString = Encoding.UTF8.GetString(receivedBuffer, 0, receivedBuffer.Length);

			// Debug.Log(subPartName + receivedString);
			if (receivedBuffer != null && receivedString.Equals("request_camera_info"))
			{
				var cameraInfoMessage = cam.GetCameraInfo();

				ClearMemoryStream(ref memoryStreamForCameraInfo);

				Serializer.Serialize<messages.CameraSensor>(memoryStreamForCameraInfo, cameraInfoMessage);

				SendResponse(memoryStreamForCameraInfo);
			}
		}
	}
}