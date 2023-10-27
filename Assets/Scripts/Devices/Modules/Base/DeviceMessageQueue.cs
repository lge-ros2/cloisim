/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Concurrent;
using System;

public class DeviceMessageQueue : BlockingCollection<DeviceMessage>
{
	private const int MaxQueue = 15;
	private const int TimeoutInMilliseconds = 200;

	public DeviceMessageQueue()
		: base(MaxQueue)
	{
	}

	public void Flush()
	{
		while (Count > 0)
		{
			Pop(out var item);
		}
	}

	private void FlushHalf()
	{
		while (Count > MaxQueue / 2)
		{
			Pop(out var item);
		}
	}

	public bool Push(in DeviceMessage data)
	{
		if (Count >= MaxQueue)
		{
			// UnityEngine.Debug.LogWarningFormat("Outbound queue is reached to maximum capacity({0})!!", MaxQueue);
			FlushHalf();
		}

		if (TryAdd(data, TimeoutInMilliseconds))
		{
			return true;
		}

		return false;
	}

	public bool Pop(out DeviceMessage item)
	{
		try
		{
			if (TryTake(out item, TimeoutInMilliseconds))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
			item = default(DeviceMessage);
		}
		return false;
	}
}
