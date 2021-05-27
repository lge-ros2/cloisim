/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System;
using UnityEngine;

public partial class DeviceTransporter : MonoBehaviour
{
	private ushort tagSize = 8;
	private int highwatermark = 1000;

	protected void DestroyTransporter()
	{
		// Debug.Log("DestroyTransporter");

		DestroyRequestSocket();

		DestroyResponseSocket();

		DestroySubscriberSocket();

		DestroyPublisherSocket();
	}

	public void SetTagSize(in ushort value)
	{
		tagSize = value;
	}

	private string GetAddress(in ushort port)
	{
		return "tcp://*:" + port;
	}

	private bool StoreTag(ref byte[] targetBuffer, in byte[] targetTag)
	{
		if (targetBuffer == null)
		{
			targetBuffer = new byte[tagSize];
		}

		if (targetTag != null)
		{
			if (targetTag.Length > tagSize || targetBuffer == null)
			{
				Debug.LogError("Failed to set hash value " + targetBuffer);
				return false;
			}
			else
			{
				Buffer.BlockCopy(targetTag, 0, targetBuffer, 0, tagSize);
			}
		}

		return true;
	}

	private bool IsNotValidTag(in byte[] receivedTag, in byte[] targetTag)
	{
		if (targetTag.Length == tagSize && receivedTag.Length == tagSize)
		{
			for (var index = 0; index < tagSize; index++)
			{
				if (targetTag[index] != receivedTag[index])
				{
					return true;
				}
			}
		}

		return false;
	}

	private bool StoreData(ref byte[] targetBuffer, in byte[] dataToStore, in int dataToStoreLength)
	{
		if (dataToStoreLength > 0 && dataToStore != null && targetBuffer != null)
		{
			var dataLength = tagSize + dataToStoreLength;

			if (dataLength > targetBuffer.Length)
			{
				Array.Resize(ref targetBuffer, dataLength);
			}

			try
			{
				Buffer.BlockCopy(dataToStore, 0, targetBuffer, tagSize, dataToStoreLength);
			}
			catch (ArgumentException ex)
			{
				Debug.LogErrorFormat("Error: BlockCopy with buffer src({0}) dst({1}) tagSize({2}) length({3}) Send() : {4}",
					dataToStore, targetBuffer, tagSize, dataToStoreLength, ex.Message);
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
		if (receivedFrame == null)
		{
			return null;
		}

		// Check Tag
		if (targetTag != null && tagSize > 0)
		{
			try
			{
				var receivedTag = new byte[tagSize];
				Buffer.BlockCopy(receivedFrame, 0, receivedTag, 0, tagSize);

				if (IsNotValidTag(receivedTag, targetTag))
				{
					Debug.LogWarning("It is Invalid Tag");
					return null;
				}
			}
			catch
			{
				Debug.LogError("Failed to check Tag just skip!!!");
			}
		}

		// Retrieve data
		var dataLength = receivedFrame.Length - tagSize;
		if (dataLength > 0)
		{
			try
			{
				var retrievedData = new byte[dataLength];
				Buffer.BlockCopy(receivedFrame, tagSize, retrievedData, 0, dataLength);
				// Debug.LogWarning("dataReceived Length - " + dataLength + "," + topic.ToString());
				return retrievedData;
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

		return null;
	}
}