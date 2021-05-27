/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class Clock : Device
{
	private messages.WorldStatistics worldStat = null;

	private double restartedSimTime = 0;
	private double restartedRealTime = 0;

	private double currentSimTime = 0;
	private double currentRealTime = 0;

	public double SimTime => currentSimTime;

	public double RealTime => currentRealTime;

	protected override void OnAwake()
	{
		Mode = ModeType.NONE;
		DeviceName = "World Clock";
	}

	protected override void InitializeMessages()
	{
		worldStat = new messages.WorldStatistics();
		worldStat.SimTime = new messages.Time();
		worldStat.PauseTime = new messages.Time();
		worldStat.RealTime = new messages.Time();
	}

	private void FixedUpdate()
	{
		currentSimTime = Time.timeAsDouble - restartedSimTime;
		currentRealTime = Time.realtimeSinceStartupAsDouble - restartedRealTime;

		DeviceHelper.SetCurrentTime(worldStat.SimTime, false);
		DeviceHelper.SetCurrentTime(worldStat.RealTime, true);
		PushDeviceMessage<messages.WorldStatistics>(worldStat);
	}

	public void ResetTime()
	{
		restartedSimTime = Time.timeAsDouble;
		restartedRealTime = Time.realtimeSinceStartupAsDouble;
	}
}
