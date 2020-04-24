/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public partial class DeviceTransporter
{
	protected bool InitializeSubscriber(in ushort targetPort)
	{
		var initialized = false;
		subscriberSocket = new SubscriberSocket();

		if (subscriberSocket != null)
		{
			subscriberSocket.Options.SendHighWatermark = highwatermark;
			subscriberSocket.Options.Linger = new TimeSpan(0);
			subscriberSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Subscriber socket connecting... " + targetPort);

		 	if (hashValueForReceive != null)
				subscriberSocket.Subscribe(hashValueForReceive);

			initialized = true;
		}

		return initialized;
	}

	protected byte[] Subscribe()
	{
		byte[] frameReceived = null;

		if (subscriberSocket != null)
		{
			frameReceived = subscriberSocket.ReceiveFrameBytes();
		}
		else
		{
			Debug.LogWarning("Socket for subscriber is not initilized yet.");
		}

		byte[] receivedData	= RetrieveData(frameReceived);
		return receivedData;
	}
}