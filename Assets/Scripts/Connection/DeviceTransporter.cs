/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.IO;
using UnityEngine;
using NetMQ.Sockets;

public partial class DeviceTransporter : MonoBehaviour
{
	public const bool isTCP = true; // Currently, NetMQ does not support UDP protocol
	private ushort tagSize = 8;
	public string defaultPipeAddress = "127.0.0.1";

	private RequestSocket requestSocket = null;
	private PublisherSocket publisherSocket = null;
	private ResponseSocket responseSocket = null;
	private SubscriberSocket subscriberSocket = null;

	private byte[] hashValueForSend = null;
	private byte[] hashValueForReceive = null;
	private byte[] dataToSend = null;

	private int highwatermark = 0;

	public DeviceTransporter()
	{
		var enviromentPipeAddress = Environment.GetEnvironmentVariable("SIM_MASTER_IP");

		if (!string.IsNullOrEmpty(enviromentPipeAddress))
		{
			defaultPipeAddress = enviromentPipeAddress;
		}

		SetPipeAddress(defaultPipeAddress);

		highwatermark = 1000;
	}

	~DeviceTransporter()
	{
		if (requestSocket != null)
		{
			requestSocket.Close();
		}

		if (responseSocket != null)
		{
			responseSocket.Close();
		}

		if (subscriberSocket != null)
		{
			subscriberSocket.Close();
		}

		if (publisherSocket != null)
		{
			publisherSocket.Close();
		}
	}

	public void SetTagSize(in ushort value)
	{
		tagSize = value;
	}

	public void SetPipeAddress(in string pipeAddress)
	{
		defaultPipeAddress = pipeAddress;
	}

	private string GetAddress(in ushort port)
	{
		return ((isTCP)?"tcp":"udp") + "://" + defaultPipeAddress + ":" + port;
	}

	public void SetHashForSend(in ulong hash)
	{
		hashValueForSend = BitConverter.GetBytes(hash);
	}

	public void SetHashForReceive(in ulong hash)
	{
		hashValueForReceive = BitConverter.GetBytes(hash);
	}

	private bool StoreTagIntoDataToSend(in byte[] targetTag)
	{
		dataToSend = new byte[tagSize];

		if (targetTag != null)
		{
			if (targetTag.Length > tagSize || dataToSend == null)
			{
				Debug.LogError("Failed to set hash value " + dataToSend);
				return false;
			}
			else
				Buffer.BlockCopy(targetTag, 0, dataToSend, 0, tagSize);
		}

		return true;
	}

	private bool IsNotValidTag(in byte[] receivedTag, in byte[] targetTag)
	{
		if (targetTag.Length == tagSize && receivedTag.Length == tagSize)
		{
			for (int index = 0; index < tagSize; index++)
				if (targetTag[index] != receivedTag[index])
					return true;
		}

		return false;
	}

	private bool StoreData(in byte[] dataToStore, in int dataToStoreLength)
	{
		if (dataToStoreLength > 0 && dataToStore != null && dataToSend != null)
		{
			var dataLength = tagSize + dataToStoreLength;
			Array.Resize(ref dataToSend, dataLength);

			try
			{
				Buffer.BlockCopy(dataToStore, 0, dataToSend, tagSize, dataToStoreLength);
			}
			catch (ArgumentException ex)
			{
				Debug.LogErrorFormat("Error: BlockCopy with buffer src({0}) dst({1}) tagSize({2}) length({3}) Send() : {4}",
					dataToStore, dataToSend, tagSize, dataToStoreLength, ex.Message);
			}
		}
		else
		{
			Debug.LogWarning("Nothing to do : " + dataToStoreLength);
			return false;
		}

		return true;
	}

	private byte[] RetrieveData(in byte[] receivedFrame, in byte[] targetTag = null)
	{
		byte[] retrievedData = null;

		if (receivedFrame != null)
		{
			if (targetTag != null && tagSize > 0)
			{
				byte[] receivedTag = new byte[tagSize];
				try
				{
					Buffer.BlockCopy(receivedFrame, 0, receivedTag, 0, tagSize);
					if (IsNotValidTag(receivedTag, targetTag))
						return null;
				}
				catch
				{
					Debug.LogError("Failed to check Tag just skip!!!");
				}
			}

			var dataLength = receivedFrame.Length - tagSize;

			if (dataLength > 0)
			{
				retrievedData = new byte[dataLength];

				try
				{
					Buffer.BlockCopy(receivedFrame, tagSize, retrievedData, 0, dataLength);
					// Debug.LogWarning("dataReceived Length - " + dataLength + "," + topic.ToString());
				}
				catch
				{
					Debug.LogError("Error: BlockCopy with buffer @ Receiver() ");
				}
			}
			else
			{
				Debug.LogWarning("Nothing received : " + receivedFrame.Length);
			}
		}

		return retrievedData;
	}

	private bool isValidMemoryStream(in MemoryStream stream)
	{
		return (stream != null && stream.CanRead && stream.Length > 0);
	}
}