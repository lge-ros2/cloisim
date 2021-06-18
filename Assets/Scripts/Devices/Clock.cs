/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public class Clock : Device
{
	private messages.WorldStatistics worldStat = null;

#region Filter times
	private messages.Time prevSimTime = new messages.Time();
	private messages.Time prevRealTime = new messages.Time();
#endregion

	private double restartedSimTime = 0;
	private double restartedRealTime = 0;

	public double currentSimTime = 0;
	public double currentRealTime = 0;

	public double SimTime => currentSimTime;

	public double RealTime => currentRealTime;

	protected override void OnAwake()
	{
		Mode = ModeType.TX_THREAD;
		DeviceName = "WorldClock";
		SetUpdateRate(20);
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
	}

	protected override void GenerateMessage()
	{
		DeviceHelper.SetCurrentTime(worldStat.SimTime);
		DeviceHelper.SetCurrentTime(worldStat.RealTime, true);

		// filter same clock info
		if (prevSimTime.Sec.Equals(worldStat.SimTime.Sec) && prevSimTime.Nsec >= worldStat.SimTime.Nsec)
		{
			// Debug.LogWarningFormat("previous sim time is same {0}.{1} >= {2}.{3}", prevSimTime.Sec, prevSimTime.Nsec, worldStat.SimTime.Sec, worldStat.SimTime.Nsec);
		}
		else if (prevRealTime.Sec.Equals(worldStat.RealTime.Sec) && prevRealTime.Nsec >= worldStat.RealTime.Nsec)
		{
			// Debug.LogWarningFormat("previous real time is same {0}.{1} >= {2}.{3}", prevRealTime.Sec, prevRealTime.Nsec, worldStat.RealTime.Sec, worldStat.RealTime.Nsec);
		}
		else
		{
			PushDeviceMessage<messages.WorldStatistics>(worldStat);
		}

		prevSimTime.Sec = worldStat.SimTime.Sec;
		prevSimTime.Nsec = worldStat.SimTime.Nsec;
		prevRealTime.Sec = worldStat.RealTime.Sec;
		prevRealTime.Nsec = worldStat.RealTime.Nsec;
	}

	public void ResetTime()
	{
		restartedSimTime = Time.timeAsDouble;
		restartedRealTime = Time.realtimeSinceStartupAsDouble;
	}
}