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
	private SensorDevices.MultiCamera multicam = null;

	protected override void OnAwake()
	{
		multicam = gameObject.GetComponent<SensorDevices.MultiCamera>();
		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var hashServiceKey = MakeHashKey("Info");
		if (!RegisterServiceDevice(hashServiceKey))
		{
			Debug.LogError("Failed to register service - " + hashServiceKey);
		}

		var hashKey = MakeHashKey();
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashKey);
		}

		AddThread(Sender);
		AddThread(Response);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (true)
		{
			if (multicam == null)
			{
				continue;
			}

			var datastreamToSend = multicam.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			multicam.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}

	private void Response()
	{
		while (true)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = CameraPlugin.ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			if (requestMessage != null)
			{
				var cameraName = requestMessage.Value.StringValue;
				var camera = multicam.GetCamera(cameraName);
				if (camera != null)
				{
					switch (requestMessage.Name)
					{
						case "request_camera_info":
							var cameraInfoMessage = camera.GetCameraInfo();
							CameraPlugin.SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);
							break;

						case "request_transform":
							var devicePosition = camera.GetPosition();
							var deviceRotation = camera.GetRotation();

							SetTransformInfoResponse(ref msForInfoResponse, devicePosition, deviceRotation);
							break;

						default:
							break;
					}
				}

				SendResponse(msForInfoResponse);
			}
		}
	}
}