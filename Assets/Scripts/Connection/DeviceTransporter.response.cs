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
	private ResponseSocket responseSocket = null;

	TimeSpan timeoutForResponse = TimeSpan.FromMilliseconds(100);

	private byte[] hashValueForReceiveRequest = null;
	private byte[] dataToSendResponse = null;

	public void SetHashForResponse(in ulong hash)
	{
		hashValueForReceiveRequest = BitConverter.GetBytes(hash);
	}

	protected bool InitializeResponsor(in ushort targetPort)
	{
		var initialized = false;
		responseSocket = new ResponseSocket();

		if (responseSocket != null)
		{
			responseSocket.Options.Linger = TimeSpan.FromTicks(0);
			responseSocket.Options.IPv4Only = true;
			responseSocket.Options.TcpKeepalive = true;
			responseSocket.Options.DisableTimeWait = true;

			responseSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Responsor socket connecting... " + targetPort);
			initialized = StoreTag(ref dataToSendResponse, hashValueForReceiveRequest);
		}

		return initialized;
	}

	protected byte[] TryReceiveRequest(in bool checkTag = false)
	{
		if (responseSocket == null)
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
			return null;
		}

		if (responseSocket.TryReceiveFrameBytes(timeoutForResponse, out var frameReceived))
		{
			var receivedData = RetrieveData(frameReceived, (checkTag)? hashValueForReceiveRequest : null);
			return receivedData;
		}

		return null;
	}

	protected byte[] ReceiveRequest(in bool checkTag = false)
	{
		if (responseSocket == null)
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
			return null;
		}

		var frameReceived = responseSocket.ReceiveFrameBytes();
		var receivedData = RetrieveData(frameReceived, (checkTag)? hashValueForReceiveRequest : null);
		return receivedData;
	}

	protected bool SendResponse(in MemoryStream streamToSend)
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

		return SendResponse(buffer, bufferLength);
	}

	protected bool SendResponse(in string stringToSend)
	{
		var buffer = System.Text.Encoding.UTF8.GetBytes(stringToSend);
		return SendResponse(buffer, stringToSend.Length);
	}

	private bool SendResponse(in byte[] buffer, in int bufferLength)
	{
		var wasSucessful = false;

		if (StoreData(ref dataToSendResponse, buffer, bufferLength) == false)
		{
			return wasSucessful;
		}

		if (responseSocket != null)
		{
			var dataLength = tagSize + bufferLength;
			wasSucessful = responseSocket.TrySendFrame(dataToSendResponse, dataLength);
		}
		else
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
		}

		return wasSucessful;
	}
}