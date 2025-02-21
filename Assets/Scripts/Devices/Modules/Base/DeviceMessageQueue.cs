/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Concurrent;
using System;

public class DeviceMessageQueue : BlockingCollection<DeviceMessage>
{
	private const int MaxQueue = 100;
	private const int TimeoutInMilliseconds = 700;
	private const float FlushLeaveRate = 0.1f;

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

		try
		{
			return TryAdd(data, TimeoutInMilliseconds);
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogWarning(ex.Message);
			return false;
		}
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
