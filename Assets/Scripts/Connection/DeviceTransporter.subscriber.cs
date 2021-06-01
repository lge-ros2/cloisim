/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
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
			subscriberSocket = null;
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
			// Console.WriteLine("Subscriber socket connecting... " + targetPort);

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
		if (subscriberSocket != null && !subscriberSocket.IsDisposed)
		{
			if (subscriberSocket.TryReceiveFrameBytes(timeoutForSubscribe, out var frameReceived))
			{
				var receivedData = RetrieveData(frameReceived);
				return receivedData;
			}
		}
		else
		{
			(Console.Out as DebugLogWriter).SetWarningOnce();
			Console.WriteLine("Socket for subscriber is not ready yet.");
		}

		return null;
	}
}