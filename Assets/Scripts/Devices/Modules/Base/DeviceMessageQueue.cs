/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Concurrent;
using System.Threading;
using System;

public sealed class DeviceMessageQueue : BlockingCollection<DeviceMessage>
{
	private const int MaxQueue = 100;
	private const int TimeoutInMilliseconds = 100;
	private const float FlushLeaveRate = 0.1f;
	private readonly int _flushThreshold;
	private CancellationTokenSource _cts;
	private int _disposed;

	public DeviceMessageQueue()
		: base(MaxQueue)
	{
		_cts = new CancellationTokenSource();
		_flushThreshold = (int)(MaxQueue * FlushLeaveRate);
	}

	protected override void Dispose(bool disposing)
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
		{
			base.Dispose(disposing);
			return;
		}

		if (disposing)
		{
			try
			{
				_cts?.Cancel();
			}
			catch { /* ignore */ }

			_cts?.Dispose();
			_cts = null;
		}

		base.Dispose(disposing);
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
			return TryAdd(data, TimeoutInMilliseconds, _cts.Token);
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
			return TryTake(out item, TimeoutInMilliseconds, _cts.Token);
		}
		catch (ObjectDisposedException)
		{
			// UnityEngine.Debug.LogWarning("ObjectDisposedException");
		}
		catch (InvalidOperationException ex)
		{
			UnityEngine.Debug.LogWarning($"InvalidOperationException - {ex.Message}");
		}
		catch (Exception ex)
		{
			_ = ex;
			// UnityEngine.Debug.LogException(ex);
		}
		item = default(DeviceMessage);
		return false;
	}
}
