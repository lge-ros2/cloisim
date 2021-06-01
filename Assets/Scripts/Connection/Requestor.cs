/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using NetMQ;
using NetMQ.Sockets;

public class Requestor : RequestSocket
{
	private byte[] hashValue = null;
	private byte[] dataToSendRequest = null;

	public void Destroy()
	{
		this.Close();
	}

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
		Options.SendHighWatermark = TransportHelper.HighWaterMark;
		Bind(TransportHelper.GetAddress(targetPort));
		// Console.WriteLine("Requester socket connecting... " + targetPort);
		return TransportHelper.StoreTag(ref dataToSendRequest, hashValue);
	}

	protected bool SendRequest(in DeviceMessage messageToSend)
	{
		if (messageToSend.IsValid())
		{
			var buffer = messageToSend.GetBuffer();
			var bufferLength = (int)messageToSend.Length;
			return SendRequest(buffer, bufferLength);
		}

		return false;
	}

	protected bool SendRequest(in string stringToSend)
	{
		var buffer = System.Text.Encoding.UTF8.GetBytes(stringToSend);
		return SendRequest(buffer, stringToSend.Length);
	}

	/// <summary>
	/// Request-response message pattern
	/// required to initialize `requestSocket`
	/// must send request message first before receive response
	/// </summary>
	/// <param name="buffer">message data buffer to send in bytes array</param>
	/// <param name="bufferLength">the length of data buffer</param>
	/// <returns>It returns false if failed to send, otherwise returns true</returns>
	public bool SendRequest(in byte[] buffer, in int bufferLength)
	{
		if (!this.IsDisposed && TransportHelper.StoreData(ref dataToSendRequest, buffer, bufferLength) )
		{
			var dataLength = TransportHelper.TagSize + bufferLength;
			return this.TrySendFrame(dataToSendRequest, dataLength);
		}
		else
		{
			(Console.Out as DebugLogWriter).SetWarningOnce();
			Console.WriteLine("Socket for request is not ready yet.");
		}

		return false;
	}

	/// <summary>
	/// Request-response message pattern
	/// required to initialize `requestSocket`
	/// must send request message first before receive response
	/// </summary>
	/// <param name="checkTag">whether to check hash tag</param>
	/// <returns>It is received bytes array data through socket without hash tag.</returns>
	public byte[] ReceiveResponse(in bool checkTag = false)
	{
		if (!this.IsDisposed)
		{
			var frameReceived = this.ReceiveFrameBytes();
			return TransportHelper.RetrieveData(frameReceived, (checkTag)? hashValue : null);;
		}
		else
		{
			(Console.Out as DebugLogWriter).SetWarningOnce();
			Console.WriteLine("Socket for request is not ready yet.");
		}

		return null;
	}
}