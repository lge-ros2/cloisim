/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Stopwatch = System.Diagnostics.Stopwatch;

public class GpsPlugin : CLOiSimPlugin
{
	private SensorDevices.GPS gps = null;

	private string hashServiceKey = string.Empty;
	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = Type.GPS;

		gps = gameObject.GetComponent<SensorDevices.GPS>();

		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(Response);
		AddThread(SenderThread);
	}

	private void SenderThread()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (gps != null)
			{
				if (gps.PopDeviceMessage(out var dataStreamToSend))
				{
					sw.Restart();
					Publish(dataStreamToSend);
					sw.Stop();
					gps.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
				}
			}
		}
	}

	private void Response()
	{
		var dmInfoResponse = new DeviceMessage();
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref dmInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_ros2":
						var topic_name = parameters.GetValue<string>("ros2/topic_name");
						var frame_id = parameters.GetValue<string>("ros2/frame_id");
						SetROS2CommonInfoResponse(ref dmInfoResponse, topic_name, frame_id);
						break;

					case "request_transform":
						var device = gps as Device;
						var devicePose = device.GetPose();
						SetTransformInfoResponse(ref dmInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(dmInfoResponse);
			}

			WaitThread();
		}
	}
}