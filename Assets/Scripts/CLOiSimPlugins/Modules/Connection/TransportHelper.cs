/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public static class TransportHelper
{
	public static ushort TagSize = 8;
	public static int HighWaterMark = 1000;

	public static string GetAddress(in ushort port)
	{
		return GetAddress() + ":" + port;
	}

	public static string GetAddress()
	{
		return "tcp://*";
	}

	public static bool StoreTag(ref byte[] targetBuffer, in byte[] targetTag)
	{
		if (targetBuffer == null)
		{
			targetBuffer = new byte[TagSize];
		}

		if (targetTag != null)
		{
			if (targetTag.Length > TagSize || targetBuffer == null)
			{
				Console.Error.WriteLine("Failed to set hash value " + targetBuffer);
				return false;
			}
			else
			{
				Buffer.BlockCopy(targetTag, 0, targetBuffer, 0, TagSize);
			}
		}

		return true;
	}

	private static bool IsNotValidTag(in byte[] receivedTag, in byte[] targetTag)
	{
		if (targetTag.Length.Equals(TagSize) && receivedTag.Length.Equals(TagSize))
		{
			for (var index = 0; index < TagSize; index++)
			{
				if (targetTag[index] != receivedTag[index])
				{
					return true;
				}
			}
		}

		return false;
	}

	public static bool StoreData(ref byte[] targetBuffer, in byte[] dataToStore, in int dataToStoreLength)
	{
		if (dataToStoreLength > 0 && dataToStore != null && targetBuffer != null)
		{
			var dataLength = TagSize + dataToStoreLength;

			if (dataLength > targetBuffer.Length)
			{
				Array.Resize(ref targetBuffer, dataLength);
			}

			try
			{
				Buffer.BlockCopy(dataToStore, 0, targetBuffer, TagSize, dataToStoreLength);
			}
			catch (ArgumentException ex)
			{
				Console.Error.WriteLine("Error: BlockCopy with buffer src({0}) dst({1}) tagSize({2}) length({3}) Send() : {4}",
					dataToStore, targetBuffer, TagSize, dataToStoreLength, ex.Message);
			}
		}
		else
		{
			Console.Error.WriteLine("Nothing to do : " + dataToStoreLength);
			return false;
		}

		return true;
	}

	public static byte[] RetrieveData(in byte[] receivedFrame, in byte[] targetTag = null)
	{
		if (receivedFrame != null)
		{
			// Check Tag
			if (targetTag != null && TagSize > 0)
			{
				try
				{
					var receivedTag = new byte[TagSize];
					Buffer.BlockCopy(receivedFrame, 0, receivedTag, 0, TagSize);

					if (IsNotValidTag(receivedTag, targetTag))
					{
						Console.Error.WriteLine("It is Invalid Tag");
						return null;
					}
				}
				catch
				{
					Console.Error.WriteLine("Failed to check Tag just skip!!!");
				}
			}

			// Retrieve data
			var dataLength = receivedFrame.Length - TagSize;
			if (dataLength > 0)
			{
				try
				{
					var retrievedData = new byte[dataLength];
					Buffer.BlockCopy(receivedFrame, TagSize, retrievedData, 0, dataLength);
					// Debug.LogWarning("dataReceived Length - " + dataLength + "," + topic.ToString());
					return retrievedData;
				}
				catch
				{
					Console.Error.WriteLine("Error: BlockCopy with buffer @ Receiver() ");
				}

			}
			else
			{
				Console.Error.WriteLine("Nothing received : " + receivedFrame.Length);
			}
		}

		return null;
	}
}