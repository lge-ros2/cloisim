/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

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
		RegisterServiceDevice(subPartName + "Info");
		RegisterTxDevice(subPartName);

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (cam != null)
			{
				var datastreamToSend = cam.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				cam.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
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
					case "request_camera_info":
						var cameraInfoMessage = cam.GetCameraInfo();
						SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);
						break;

					case "request_transform":
						var devicePose = cam.GetPose();
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
}