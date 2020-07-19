/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;

public partial class DeviceTransporter
{
	private PublisherSocket publisherSocket = null;

	private byte[] hashValueForPublish = null;
	private byte[] dataToPublish = null;

	public void SetHashForPublish(in ulong hash)
	{
		hashValueForPublish = BitConverter.GetBytes(hash);
	}

	protected bool InitializePublisher(in ushort targetPort)
	{
		var initialized = false;
		publisherSocket = new PublisherSocket();

		if (publisherSocket != null)
		{
			publisherSocket.Options.TcpKeepalive = true;
			publisherSocket.Options.SendHighWatermark = highwatermark;
			publisherSocket.Options.Linger = new TimeSpan(0);
			publisherSocket.Bind(GetAddress(targetPort));
			// Debug.Log("Publisher socket binding for - " + targetPort);
			initialized = StoreTag(ref dataToPublish, hashValueForPublish);
		}

		return initialized;
	}

	protected bool Publish(in MemoryStream streamToSend)
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

		return Publish(buffer, bufferLength);
	}

	protected bool Publish(in string stringToSend)
	{
		var buffer = System.Text.Encoding.UTF8.GetBytes(stringToSend);
		return Publish(buffer,stringToSend.Length);
	}

	protected bool Publish(in byte[] bufferToSend, in int bufferLength)
	{
		bool wasSucessful = false;

		if (StoreData(ref dataToPublish, bufferToSend, bufferLength) == false)
		{
			return wasSucessful;
		}

		if (publisherSocket != null)
		{
			wasSucessful = publisherSocket.TrySendFrame(dataToPublish);
			// Debug.LogFormat("Publish data({0}) length({1})", bufferToSend, bufferLength);
		}
		else
		{
			Debug.LogWarning("Socket for publisher or response-request is not initilized yet.");
		}

		return wasSucessful;
	}
}