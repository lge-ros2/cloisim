/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class MultiCameraPlugin : DevicePlugin
{
	private MemoryStream memoryStreamForCameraInfo = null;
	private SensorDevices.MultiCamera cam = null;

	protected override void OnAwake()
	{
		memoryStreamForCameraInfo = new MemoryStream();
		cam = gameObject.GetComponent<SensorDevices.MultiCamera>();
		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var hashServiceKey = MakeHashKey(partName, "Info");
		if (!RegisterServiceDevice(hashServiceKey))
		{
			Debug.LogError("Failed to register ElevatorSystem service - " + hashServiceKey);
		}

		var hashKey = MakeHashKey(partName);
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashKey);
		}

		AddThread(Sender);
		AddThread(Response);
	}

	void OnDestroy()
	{
		memoryStreamForCameraInfo.Dispose();
	}

	private void Sender()
	{
		var sw = new Stopwatch();
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

			var requestMessage = CameraPlugin.ParsingCameraInfoRequest(ref memoryStreamForCameraInfo, receivedBuffer);

			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_camera_info":

						var cameraName = requestMessage.Value.StringValue;
						if (cameraName != null)
						{
							var cameraInfoMessage = cam.GetCameraInfo(cameraName);

							CameraPlugin.SetCameraInfoResponse(ref memoryStreamForCameraInfo, cameraInfoMessage);
						}

						SendResponse(memoryStreamForCameraInfo);

						break;

					default:
						break;
				}
			}
		}
	}
}