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
	private const int TimeoutInMilliseconds = 100;
	private const float FlushLeaveRate = 0.1f;
	private readonly int _flushThreshold;

	public DeviceMessageQueue()
		: base(MaxQueue)
	{
		_flushThreshold = (int)(MaxQueue * FlushLeaveRate);
	}

	public void Flush()
	{
		while (TryTake(out _)) { };
	}

	private void FlushPortion()
	{
		var currentCount = Count;
		while (currentCount-- > _flushThreshold && TryTake(out _)) { };
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
			return false;
		}
	}
}
