/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System;
using ProtoBuf;

public class DeviceMessage : MemoryStream
{
	public DeviceMessage()
	{
		Reset();
	}

	public bool SetMessage(in byte[] data)
	{
		if (data == null)
		{
			return false;
		}

		if (CanWrite)
		{
			Reset();
			Write(data, 0, data.Length);
			Position = 0;
		}
		else
		{
			Console.WriteLine("Failed to write memory stream");
		}

		return true;
	}

	public void SetMessage<T>(T instance)
	{
		if (CanWrite)
		{
			Reset();
			try
			{
				Serializer.Serialize<T>(this, instance);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: SetMessage<{typeof(T).ToString()}>() during Serializer.Serialize: {ex.Message}");
			}
		}
		else
		{
			Console.WriteLine("Failed to write memory stream");
		}
	}

	public T GetMessage<T>()
	{
		Position = 0;

		T result;
		try
		{
			result = Serializer.Deserialize<T>(this);
		}
		catch (Exception)
		{
			result = default(T);
		}

		return result;
	}

	public void Reset()
	{
		Flush();
		SetLength(0);
		Position = 0;
		Capacity = 0;
	}

	public bool IsValid()
	{
		return (CanRead && Length > 0);
	}
}