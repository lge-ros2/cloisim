/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.Threading;
using Stopwatch = System.Diagnostics.Stopwatch;

public class LaserPlugin : DevicePlugin
{
	private SensorDevices.Lidar lidar = null;

	protected override void OnAwake()
	{
		partName = DeviceHelper.GetPartName(gameObject);
		lidar = gameObject.GetComponent<SensorDevices.Lidar>();
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
			Debug.LogError("Failed to register for LaserPlugin - " + hashKey);
		}

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (lidar == null)
			{
				continue;
			}

			var datastreamToSend = lidar.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			lidar.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}

	private void Response()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			if (requestMessage != null)
			{
				var device = lidar as Device;

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