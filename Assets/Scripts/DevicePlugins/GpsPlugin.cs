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

	private string hashServiceKey = string.Empty;
	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		gps = gameObject.GetComponent<SensorDevices.GPS>();

		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice();

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (gps != null)
			{
				var datastreamToSend = gps.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				gps.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
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