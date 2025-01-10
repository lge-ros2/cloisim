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
	private readonly object _lock = new object();

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

			lock (_lock)
			{
				Write(data, 0, data.Length);
				Position = 0;
			}
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

			lock (_lock)
			{
				Seek(0, SeekOrigin.Begin);
				Serializer.Serialize<T>(this, instance);
			}
		}
		else
		{
			Console.WriteLine("Failed to write memory stream");
		}
	}

	public T GetMessage<T>()
	{
		T result;

		try
		{
			lock (_lock)
			{
				Seek(0, SeekOrigin.Begin);
				result = Serializer.Deserialize<T>(this);
			}
		}
		catch (Exception)
		{
			result = default(T);
		}

		return result;
	}

	public void Reset()
	{
		lock (_lock)
		{
			Flush();
			SetLength(0);
			Position = 0;
			Capacity = 0;
		}
	}

	public bool IsValid()
	{
		return (CanRead && Length > 0);
	}
}