/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using NetMQ;
using NetMQ.Sockets;

public class Responsor : ResponseSocket
{
	private TimeSpan timeout = TimeSpan.FromMilliseconds(500);

	private byte[] hashValue = null;
	private byte[] dataToSendResponse = null;

	public Responsor(in ulong hash)
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
		Bind(TransportHelper.GetAddress(targetPort));
		// Debug.Log("Responsor socket connecting... " + targetPort);

		return TransportHelper.StoreTag(ref dataToSendResponse, hashValue);
	}

	public byte[] ReceiveRequest(in bool checkTag = false)
	{
		if (!IsDisposed)
		{
			if (this.TryReceiveFrameBytes(timeout, out var frameReceived))
			{
				return TransportHelper.RetrieveData(frameReceived, (checkTag) ? hashValue : null);
			}
		}
		else
		{
			Console.Error.WriteLine("Socket for response is not ready.");
		}
		return null;
	}

	public bool SendResponse(in DeviceMessage messageToSend)
	{
		if (!messageToSend.IsValid())
		{
			return false;
		}
		var buffer = messageToSend.GetBuffer();
		var bufferLength = (int)messageToSend.Length;
		return SendResponse(buffer, bufferLength);
	}

	public bool SendResponse(in string stringToSend)
	{
		var buffer = System.Text.Encoding.UTF8.GetBytes(stringToSend);
		return SendResponse(buffer, stringToSend.Length);
	}

	public bool SendResponse(in byte[] buffer, in int bufferLength)
	{
		if (TransportHelper.StoreData(ref dataToSendResponse, buffer, bufferLength))
		{
			var dataLength = TransportHelper.TagSize + bufferLength;
			return this.TrySendFrame(dataToSendResponse, dataLength);
		}
		else
		{
			Console.Error.WriteLine("Socket for response is not ready yet.");
		}

		return false;
	}
}