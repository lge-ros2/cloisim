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
		RegisterServiceDevice("Info");
		RegisterTxDevice();

		AddThread(Sender);
		AddThread(Response);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (multicam != null)
			{
				var datastreamToSend = multicam.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				multicam.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
		}
	}

	private void Response()
	{
		while (IsRunningThread)
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
							var devicePose = camera.GetPose();
							SetTransformInfoResponse(ref msForInfoResponse, devicePose);
							break;

						default:
							break;
					}
				}

				SendResponse(msForInfoResponse);
			}

			ThreadWait();
		}
	}
}