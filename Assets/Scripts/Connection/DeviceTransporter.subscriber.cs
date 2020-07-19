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
	private SubscriberSocket subscriberSocket = null;

	private byte[] hashValueForSubscription = null;

	protected bool InitializeSubscriber(in ushort targetPort)
	{
		var initialized = false;
		subscriberSocket = new SubscriberSocket();

		if (subscriberSocket != null)
		{
			subscriberSocket.Options.TcpKeepalive = true;
			subscriberSocket.Options.ReceiveHighWatermark = highwatermark;
			subscriberSocket.Options.Linger = new TimeSpan(0);

		 	if (hashValueForSubscription != null)
			{
				subscriberSocket.Subscribe(hashValueForSubscription);
			}

			subscriberSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Subscriber socket connecting... " + targetPort);

			initialized = true;
		}

		return initialized;
	}

	public void SetHashForSubscription(in ulong hash)
	{
		hashValueForSubscription = BitConverter.GetBytes(hash);
	}

	protected byte[] Subscribe()
	{
		if (subscriberSocket != null)
		{
			var frameReceived = subscriberSocket.ReceiveFrameBytes();
			var receivedData = RetrieveData(frameReceived);
			return receivedData;
		}
		else
		{
			Debug.LogWarning("Socket for subscriber is not initilized yet.");
		}

		return null;
	}
}