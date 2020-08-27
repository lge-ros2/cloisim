/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class GpsPlugin : DevicePlugin
{
	private SensorDevices.GPS gps = null;

	protected override void OnAwake()
	{
		gps = gameObject.GetComponent<SensorDevices.GPS>();
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
			Debug.LogError("Failed to register for GpsPlugin - " + hashKey);
		}

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (gps == null)
			{
				continue;
			}

			var datastreamToSend = gps.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			gps.SetTransportTime((float)sw.Elapsed.TotalSeconds);
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
				var device = gps as Device;

				switch (requestMessage.Name)
				{
					case "request_transform":
						var devicePose = device.GetPose();
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