/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;
using System.Text;
using messages = cloisim.msgs;

public class Clock : Device
{
	private messages.WorldStatistics worldStat = null;

#region Filter times
	private double prevSimTime = 0f;
	private double prevRealTime = 0f;
#endregion

	private double restartedSimTime = 0;
	private double restartedRealTime = 0;

	private double currentSimTime = 0;
	private double currentRealTime = 0;

#region time in hms format
	public class HMS
	{
		private StringBuilder simTime = new StringBuilder(18);
		private StringBuilder realTime = new StringBuilder(18);
		private StringBuilder diffTime = new StringBuilder(18);

		public void SetSimTime(in TimeSpan ts)
		{
			SetTimeString(ref this.simTime, ts);
		}

		public void SetRealTime(in TimeSpan ts)
		{
			SetTimeString(ref this.realTime, ts);
		}

		public void SetDiffTime(in TimeSpan ts)
		{
			SetTimeString(ref this.diffTime, ts);
		}

		private void SetTimeString(ref StringBuilder target, in TimeSpan ts)
		{
			target.Clear();
			target.Append(ts.Days.ToString());
			target.Append("d ");
			target.Append(ts.Hours.ToString());
			target.Append(":");
			target.Append(ts.Minutes.ToString());
			target.Append(":");
			target.Append(ts.Seconds.ToString());
			target.Append(".");
			target.Append(ts.Milliseconds.ToString());
		}

		public string SimTime => simTime.ToString();
		public string RealTime => realTime.ToString();
		public string DiffTime => diffTime.ToString();
	}

	private HMS hms = new HMS();
#endregion

	public double SimTime => currentSimTime;

	public double RealTime => currentRealTime;

	public HMS ToHMS() => hms;

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

	void FixedUpdate()
	{
		currentSimTime = Time.timeAsDouble - restartedSimTime;
		currentRealTime = Time.realtimeSinceStartupAsDouble - restartedRealTime;
	}

	void LateUpdate()
	{
		var simTs = TimeSpan.FromSeconds(SimTime);
		var realTs = TimeSpan.FromSeconds(RealTime);
		var diffTs = realTs - simTs;

		hms.SetSimTime(simTs);
		hms.SetRealTime(realTs);
		hms.SetDiffTime(diffTs);
	}

	protected override void GenerateMessage()
	{
		DeviceHelper.SetCurrentTime(worldStat.SimTime);
		DeviceHelper.SetCurrentTime(worldStat.RealTime, true);

		// filter same clock info
		if (prevSimTime >= SimTime)
		{
			// Debug.LogWarningFormat("Filter SimTime, Prev:{0} >= Current:{1}", prevSimTime, SimTime);
		}
		else if (prevRealTime >= RealTime)
		{
			// Debug.LogWarningFormat("Filter RealTime, Prev:{0} >= Current:{1}", prevRealTime, RealTime);
		}
		else
		{
			PushDeviceMessage<messages.WorldStatistics>(worldStat);
			prevSimTime = SimTime;
			prevRealTime = RealTime;
		}
	}

	public void ResetTime()
	{
		restartedSimTime = Time.timeAsDouble;
		restartedRealTime = Time.realtimeSinceStartupAsDouble;
	}
}