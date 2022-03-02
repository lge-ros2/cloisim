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
		private string _simTime = string.Empty;
		private string _realTime = string.Empty;
		private string _diffTime = string.Empty;

		public void SetSimTime(in TimeSpan ts)
		{
			SetTimeString(ref this._simTime, ts);
		}

		public void SetRealTime(in TimeSpan ts)
		{
			SetTimeString(ref this._realTime, ts);
		}

		public void SetDiffTime(in TimeSpan ts)
		{
			SetTimeString(ref this._diffTime, ts);
		}

		private StringBuilder _tempSB = new StringBuilder(16);

		private void SetTimeString(ref string target, in TimeSpan ts)
		{
			_tempSB.AppendFormat("{0}d {1}:{2}:{3}.{4}", ts.Days.ToString(), ts.Hours.ToString(), ts.Minutes.ToString(), ts.Seconds.ToString(), ts.Milliseconds.ToString());
			target = _tempSB.ToString();
			_tempSB.Clear();
		}

		public string SimTime => _simTime;
		public string RealTime => _realTime;
		public string DiffTime => _diffTime;
	}

	private HMS hms = new HMS();

	private int hmsUpdateIndex = 0;
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
		currentRealTime = Time.realtimeSinceStartupAsDouble - restartedRealTime;
		currentSimTime = Time.timeAsDouble - restartedSimTime;
	}

	void LateUpdate()
	{
		var simTs = TimeSpan.FromSeconds(SimTime);
		var realTs = TimeSpan.FromSeconds(RealTime);
		var diffTs = realTs - simTs;

		switch (hmsUpdateIndex++)
		{
			case 0:
				hms.SetSimTime(simTs);
				break;
			case 1:
				hms.SetRealTime(realTs);
				break;
			case 2:
				hms.SetDiffTime(diffTs);
				break;
			default:
				// skip
				break;
		}
		hmsUpdateIndex %= 3;
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