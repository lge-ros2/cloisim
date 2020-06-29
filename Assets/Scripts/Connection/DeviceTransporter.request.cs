/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public partial class DeviceTransporter
{
	protected bool InitializeRequester(in ushort targetPort)
	{
		var initialized = false;
		requestSocket = new RequestSocket();

		if (requestSocket != null)
		{
			requestSocket.Options.TcpKeepalive = true;
			requestSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Requester socket connecting... " + targetPort);
			initialized = StoreTagIntoDataToSend(hashValueForSend);
		}

		return initialized;
	}

	protected bool SendRequest(in MemoryStream streamToSend)
	{
		if (isValidMemoryStream(streamToSend) == false)
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
	/// <param name="bytesToSend">message data buffer to send in bytes array</param>
	/// <param name="bytesLength">the length of data buffer</param>
	/// <returns>It returns false if failed to send, otherwise returns true</returns>
	protected bool SendRequest(in byte[] bytesToSend, in int bytesLength)
	{
		bool wasSucessful = false;

		if (StoreData(bytesToSend, bytesLength) == false)
		{
			return wasSucessful;
		}

		if (requestSocket != null)
		{
			wasSucessful = requestSocket.TrySendFrame(dataToSend);
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
		byte[] frameReceived = null;

		if (requestSocket != null)
		{
			frameReceived = requestSocket.ReceiveFrameBytes();
		}
		else
		{
			Debug.LogWarning("Socket for request is not initilized yet.");
		}

		byte[] receivedData = RetrieveData(frameReceived, (checkTag)? hashValueForSend : null);
		return receivedData;
	}
}