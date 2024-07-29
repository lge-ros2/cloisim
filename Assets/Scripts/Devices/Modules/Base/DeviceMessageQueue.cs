/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Concurrent;
using System;

public class DeviceMessageQueue : BlockingCollection<DeviceMessage>
{
	private const int MaxQueue = 30;
	private const int TimeoutInMilliseconds = 500;
	private const float FlushLeaveRate = 0.3f;

	public DeviceMessageQueue()
		: base(MaxQueue)
	{
	}

	public void Flush()
	{
		while (Count > 0)
		{
			Pop(out var _);
		}
	}

	private void FlushPortion()
	{
		while (Count > (int)(MaxQueue * FlushLeaveRate))
		{
			Pop(out var _);
		}
	}

	public bool Push(in DeviceMessage data)
	{
		if (Count >= MaxQueue)
		{
			// UnityEngine.Debug.LogWarning($"Outbound queue is reached to maximum capacity({MaxQueue})!!");
			FlushPortion();
		}

		return TryAdd(data, TimeoutInMilliseconds);
	}

	public bool Pop(out DeviceMessage item)
	{
		try
		{
			return TryTake(out item, TimeoutInMilliseconds);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
			item = default(DeviceMessage);
		}
		return false;
	}
}
