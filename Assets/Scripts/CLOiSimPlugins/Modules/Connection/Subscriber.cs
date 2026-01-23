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
	private TimeSpan _timeout = TimeSpan.FromMilliseconds(100);

	private byte[] _hashValue = null;

	public Subscriber(in ulong hash)
	{
		SetHash(hash);
	}

	private void SetHash(in ulong hash)
	{
		_hashValue = BitConverter.GetBytes(hash);
	}

	public bool Initialize(in ushort targetPort)
	{
		Options.Linger = TimeSpan.FromTicks(0);
		Options.IPv4Only = true;
		Options.TcpKeepalive = true;
		Options.DisableTimeWait = false;
		Options.ReceiveHighWatermark = TransportHelper.HighWaterMark;

		if (_hashValue != null)
		{
			this.Subscribe(_hashValue);
		}

		Bind(TransportHelper.GetAddress(targetPort));
		// Console.WriteLine("Subscriber socket connecting... " + targetPort);

		return true;
	}

	public byte[] Subscribe()
	{
		if (IsDisposed)
		{
			Console.Error.WriteLine("Socket for subscriber is not ready yet.");
		}
		else
		{
			if (this.TryReceiveFrameBytes(_timeout, out var frameReceived))
			{
				// Console.Error.WriteLine(frameReceived.Length);
				return TransportHelper.RetrieveData(frameReceived);
			}
		}

		return null;
	}
}