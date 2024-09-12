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

		Reset();

		lock (this)
		{
			if (CanWrite)
			{
				Write(data, 0, data.Length);
				Position = 0;
			}
			else
			{
				Console.WriteLine("Failed to write memory stream");
			}
		}
		return true;
	}

	public void SetMessage<T>(T instance)
	{
		Reset();

		lock (this)
		{
			Serializer.Serialize<T>(this, instance);
		}
	}

	public T GetMessage<T>()
	{
		T result;

		lock (this)
		{
			Seek(0, SeekOrigin.Begin);
			try
			{
				result = Serializer.Deserialize<T>(this);
			}
			catch (Exception)
			{
				result = default(T);
			}
		}

		return result;
	}

	public void Reset()
	{
		lock (this)
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