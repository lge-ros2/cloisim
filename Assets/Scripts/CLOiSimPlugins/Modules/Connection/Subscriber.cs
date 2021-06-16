/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using NetMQ;
using NetMQ.Sockets;

public class Subscriber : SubscriberSocket
{
	private TimeSpan timeout = TimeSpan.FromMilliseconds(500);

	private byte[] hashValue = null;

	public void SetHash(in ulong hash)
	{
		hashValue = BitConverter.GetBytes(hash);
	}

	public bool Initialize(in ushort targetPort)
	{
		Options.Linger = TimeSpan.FromTicks(0);
		Options.IPv4Only = true;
		Options.TcpKeepalive = true;
		Options.DisableTimeWait = true;
		Options.ReceiveHighWatermark = TransportHelper.HighWaterMark;

		if (hashValue != null)
		{
			this.Subscribe(hashValue);
		}

		Bind(TransportHelper.GetAddress(targetPort));
		// Console.WriteLine("Subscriber socket connecting... " + targetPort);

		return true;
	}


	public byte[] Subscribe()
	{
		if (IsDisposed)
		{
			(Console.Out as DebugLogWriter).SetWarningOnce();
			Console.WriteLine("Socket for subscriber is not ready yet.");
		}
		else
		{
			if (this.TryReceiveFrameBytes(timeout, out var frameReceived))
			{
				return TransportHelper.RetrieveData(frameReceived);
			}
		}

		return null;
	}
}