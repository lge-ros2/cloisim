/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.IO;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public partial class DeviceTransporter
{
	private RequestSocket requestSocket = null;

	private byte[] hashValueForSendRequest = null;
	private byte[] dataToSendRequest = null;

	private void DestroyRequestSocket()
	{
		if (requestSocket != null)
		{
			requestSocket.Close();
		}
	}

	public void SetHashForRequest(in ulong hash)
	{
		hashValueForSendRequest = BitConverter.GetBytes(hash);
	}

	protected bool InitializeRequester(in ushort targetPort)
	{
		var initialized = false;
		requestSocket = new RequestSocket();

		if (requestSocket != null)
		{
			requestSocket.Options.Linger = TimeSpan.FromTicks(0);
			requestSocket.Options.IPv4Only = true;
			requestSocket.Options.TcpKeepalive = true;
			requestSocket.Options.DisableTimeWait = true;
			requestSocket.Options.SendHighWatermark = highwatermark;

			requestSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Requester socket connecting... " + targetPort);
			initialized = StoreTag(ref dataToSendRequest, hashValueForSendRequest);
		}

		return initialized;
	}

	protected bool SendRequest(in MemoryStream streamToSend)
	{
		if (!isValidMemoryStream(streamToSend))
		{
			return false;
		}

		byte[] buffer = null;
		int bufferLength = 0;

		lock (streamToSend)
		{
			buffer = streamToSend.GetBuffer();
			bufferLength = (int)streamToSend.Length;
		}

		return SendRequest(buffer, bufferLength);
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
	protected bool SendRequest(in byte[] buffer, in int bufferLength)
	{
		bool wasSucessful = false;

		if (StoreData(ref dataToSendRequest, buffer, bufferLength) == false)
		{
			return wasSucessful;
		}

		if (requestSocket != null)
		{
			var dataLength = tagSize + bufferLength;
			wasSucessful = requestSocket.TrySendFrame(dataToSendRequest, dataLength);
		}
		else
		{
			Debug.LogWarning("Socket for request is not initilized yet.");
		}

		return wasSucessful;
	}

	/// <summary>
	/// Request-response message pattern
	/// required to initialize `requestSocket`
	/// must send request message first before receive response
	/// </summary>
	/// <param name="checkTag">whether to check hash tag</param>
	/// <returns>It is received bytes array data through socket without hash tag.</returns>
	protected byte[] ReceiveResponse(in bool checkTag = false)
	{
		if (requestSocket != null)
		{
			var frameReceived = requestSocket.ReceiveFrameBytes();
			var receivedData = RetrieveData(frameReceived, (checkTag)? hashValueForSendRequest : null);
			return receivedData;
		}
		else
		{
			Debug.LogWarning("Socket for request is not initilized yet.");
		}

		return null;
	}
}