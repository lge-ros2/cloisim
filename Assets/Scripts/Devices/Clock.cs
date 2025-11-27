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
	private double _prevSimTime = 0f;
	private double _prevRealTime = 0f;
	#endregion

	private double _restartedSimTime = 0;
	private double _restartedFixedSimTime = 0;
	private double _restartedRealTime = 0;

	private double _currentSimTime = 0;
	private double _currentFixedSimTime = 0;
	private double _currentRealTime = 0;

	#region time in _hms format
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
			SetTimeString(ref this._diffTime, ts, true);
		}

		private StringBuilder _tempSB = new StringBuilder(16);

		private void SetTimeString(ref string target, in TimeSpan ts, in bool secondsOnly = false)
		{
			var timeString = (secondsOnly)?
				$"{ts.TotalSeconds}" :
				$"{ts.Days}d {ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";

			_tempSB.AppendFormat(timeString);
			target = _tempSB.ToString();
			_tempSB.Clear();
		}

		public string SimTime => _simTime;
		public string RealTime => _realTime;
		public string DiffTime => _diffTime;
	}

	private HMS _hms = new HMS();

	private int _hmsUpdateIndex = 0;
	#endregion

	public double SimTime => _currentSimTime;
	public double FixedSimTime => _currentFixedSimTime;
	public double RealTime => _currentRealTime;

	public HMS ToHMS() => _hms;

	protected override void OnAwake()
	{
		Mode = ModeType.TX_THREAD;
		DeviceName = "WorldClock";
		SetUpdateRate(50);
	}

	protected override void InitializeMessages()
	{
		worldStat = new messages.WorldStatistics();
		worldStat.SimTime = new messages.Time();
		worldStat.PauseTime = new messages.Time();
		worldStat.RealTime = new messages.Time();
	}

	void Update()
	{
		_currentRealTime = Time.realtimeSinceStartupAsDouble - _restartedRealTime;
		_currentSimTime = Time.timeAsDouble - _restartedSimTime;
	}

	void FixedUpdate()
	{
		_currentFixedSimTime = Time.fixedTimeAsDouble - _restartedFixedSimTime;
	}

	void LateUpdate()
	{
		var simTs = TimeSpan.FromSeconds(SimTime);
		var realTs = TimeSpan.FromSeconds(RealTime);
		var diffTs = realTs - simTs;

		switch (_hmsUpdateIndex++)
		{
			case 0:
				_hms.SetSimTime(simTs);
				break;
			case 1:
				_hms.SetRealTime(realTs);
				break;
			case 2:
				_hms.SetDiffTime(diffTs);
				break;
			default:
				// skip
				break;
		}
		_hmsUpdateIndex %= 3;
	}

	protected override void GenerateMessage()
	{
		worldStat.SimTime.SetCurrentTime();
		worldStat.RealTime.SetCurrentTime(true);

		// filter same clock info
		if (_prevSimTime >= SimTime)
		{
			if (_prevSimTime > SimTime)
			{
				Debug.LogWarning($"Filter SimTime, Prev:{_prevSimTime} >= Current:{SimTime}");
			}
		}
		else if (_prevRealTime >= RealTime)
		{
			if (_prevRealTime > RealTime)
			{
				Debug.LogWarning($"Filter RealTime, Prev:{_prevRealTime} >= Current:{RealTime}");
			}
		}
		else
		{
			PushDeviceMessage<messages.WorldStatistics>(worldStat);
			_prevSimTime = SimTime;
			_prevRealTime = RealTime;
		}
	}

	public void ResetTime()
	{
		_restartedSimTime = Time.timeAsDouble;
		_restartedFixedSimTime = Time.fixedTimeAsDouble;
		_restartedRealTime = Time.realtimeSinceStartupAsDouble;

		_prevSimTime = SimTime;
		_prevRealTime = RealTime;
	}
}