/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public partial class DeviceTransporter
{
	private ResponseSocket responseSocket = null;

	private TimeSpan timeoutForResponse = TimeSpan.FromMilliseconds(500);

	private byte[] hashValueForReceiveRequest = null;
	private byte[] dataToSendResponse = null;

	private void DestroyResponseSocket()
	{
		if (responseSocket != null)
		{
			responseSocket.Close();
		}
	}

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

	protected byte[] ReceiveRequest(in bool checkTag = false)
	{
		if (responseSocket == null)
		{
			Debug.LogWarning("Socket for response is not initilized yet.");
			return null;
		}

		if (responseSocket.TryReceiveFrameBytes(timeoutForResponse, out var frameReceived))
		{
			return RetrieveData(frameReceived, (checkTag)? hashValueForReceiveRequest : null);
		}

		return null;
	}

	protected bool SendResponse(in DeviceMessage messageToSend)
	{
		if (messageToSend.IsValid())
		{
			var buffer = messageToSend.GetBuffer();
			var bufferLength = (int)messageToSend.Length;
			return SendResponse(buffer, bufferLength);
		}
		return false;
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