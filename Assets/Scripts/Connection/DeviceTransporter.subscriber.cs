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

	private TimeSpan timeoutForSubscribe = TimeSpan.FromMilliseconds(500);

	private byte[] hashValueForSubscription = null;

	private void DestroySubscriberSocket()
	{
		if (subscriberSocket != null)
		{
			subscriberSocket.Close();
		}
	}

	protected bool InitializeSubscriber(in ushort targetPort)
	{
		var initialized = false;
		subscriberSocket = new SubscriberSocket();

		if (subscriberSocket != null)
		{
			subscriberSocket.Options.Linger = TimeSpan.FromTicks(0);
			subscriberSocket.Options.IPv4Only = true;
			subscriberSocket.Options.TcpKeepalive = true;
			subscriberSocket.Options.DisableTimeWait = true;
			subscriberSocket.Options.ReceiveHighWatermark = highwatermark;

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
			if (subscriberSocket.TryReceiveFrameBytes(timeoutForSubscribe, out var frameReceived))
			{
				var receivedData = RetrieveData(frameReceived);
				return receivedData;
			}
		}
		else
		{
			Debug.LogWarning("Socket for subscriber is not initilized yet.");
		}

		return null;
	}
}