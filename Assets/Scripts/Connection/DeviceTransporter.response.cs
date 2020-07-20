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
			responseSocket.Options.TcpKeepalive = true;
			responseSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Responsor socket connecting... " + targetPort);
			initialized = StoreTag(ref dataToSendResponse, hashValueForReceiveRequest);
		}

		return initialized;
	}

	protected byte[] ReceiveRequest(in bool checkTag = false)
	{
		byte[] frameReceived = null;

		if (responseSocket != null)
		{
			frameReceived = responseSocket.ReceiveFrameBytes();
		}
		else
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
		}

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

	private bool SendResponse(in byte[] bytesToSend, in int bytesLength)
	{
		var wasSucessful = false;

		if (StoreData(ref dataToSendResponse, bytesToSend, bytesLength) == false)
		{
			return wasSucessful;
		}

		if (responseSocket != null)
		{
			wasSucessful = responseSocket.TrySendFrame(dataToSendResponse);
		}
		else
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
		}

		return wasSucessful;
	}
}