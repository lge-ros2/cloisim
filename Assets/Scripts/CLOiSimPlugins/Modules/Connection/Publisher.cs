/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using NetMQ;
using NetMQ.Sockets;

public class Publisher : PublisherSocket
{
	private byte[] hashValue = null;
	private byte[] dataToPublish = null;

	public Publisher(in ulong hash)
	{
		SetHash(hash);
	}

	private void SetHash(in ulong hash)
	{
		hashValue = BitConverter.GetBytes(hash);
	}

	public bool Initialize(in ushort targetPort)
	{
		Options.Linger = TimeSpan.FromTicks(0);
		Options.IPv4Only = true;
		Options.TcpKeepalive = true;
		Options.DisableTimeWait = true;
		Options.SendHighWatermark = TransportHelper.HighWaterMark;
		Bind(TransportHelper.GetAddress(targetPort));
		// publisherSocket.BindRandomPort(TransportHelper.GetAddress());
		// Console.WriteLine("Publisher socket binding for - " + targetPort);

		return TransportHelper.StoreTag(ref dataToPublish, hashValue);
	}

	public bool Publish(in DeviceMessage messageToSend)
	{
		if (messageToSend.IsValid())
		{
			var buffer = messageToSend.GetBuffer();
			var bufferLength = (int)messageToSend.Length;
			return Publish(buffer, bufferLength);
		}

		return false;
	}

	public bool Publish(in string stringToSend)
	{
		var buffer = System.Text.Encoding.UTF8.GetBytes(stringToSend);
		return Publish(buffer, stringToSend.Length);
	}

	public bool Publish(in byte[] buffer, in int bufferLength)
	{
		var wasSucessful = false;

		if (TransportHelper.StoreData(ref dataToPublish, buffer, bufferLength))
		{
			if (!IsDisposed)
			{
				var dataLength = TransportHelper.TagSize + bufferLength;
				wasSucessful = this.TrySendFrame(dataToPublish, dataLength);
				// Debug.LogFormat("Publish data({0}) length({1})", buffer, bufferLength);
			}
			else
			{
				(Console.Out as DebugLogWriter).SetWarningOnce();
				Console.WriteLine("Socket for publisher is not ready yet.");
			}
		}

		return wasSucessful;
	}
}