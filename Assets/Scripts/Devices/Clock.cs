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
	private double _deltaTime = 0;
	private double _fixedDeltaTime = 0;

	private bool _isSecondsOnly = false;

	#region time in _hms format
	public class HMS
	{
		private string _simTime = string.Empty;
		private string _realTime = string.Empty;
		private string _diffTime = string.Empty;

		public void SetSimTime(in TimeSpan ts, in bool secondsOnly = false)
		{
			SetTimeString(ref this._simTime, ts, secondsOnly);
		}

		public void SetRealTime(in TimeSpan ts, in bool secondsOnly = false)
		{
			SetTimeString(ref this._realTime, ts, secondsOnly);
		}

		public void SetDiffTime(in TimeSpan ts)
		{
			SetTimeString(ref this._diffTime, ts, true);
		}

		private StringBuilder _tempSB = new StringBuilder(16);

		private void SetTimeString(ref string target, in TimeSpan ts, in bool secondsOnly = false)
		{
			_tempSB.Clear();
			if (secondsOnly)
			{
				_tempSB.Append(ts.TotalSeconds);
			}
			else
			{
				_tempSB.Append(ts.Days).Append("d ");
				AppendTwoDigits(ts.Hours);
				_tempSB.Append(':');
				AppendTwoDigits(ts.Minutes);
				_tempSB.Append(':');
				AppendTwoDigits(ts.Seconds);
				_tempSB.Append('.');
				AppendThreeDigits(ts.Milliseconds);
			}
			target = _tempSB.ToString();
		}

		private void AppendTwoDigits(int value)
		{
			if (value < 10) _tempSB.Append('0');
			_tempSB.Append(value);
		}

		private void AppendThreeDigits(int value)
		{
			if (value < 10) _tempSB.Append("00");
			else if (value < 100) _tempSB.Append('0');
			_tempSB.Append(value);
		}

		public string SimTime => _simTime;
		public string RealTime => _realTime;
		public string DiffTime => _diffTime;
	}

	private HMS _hms = new();

	private int _hmsUpdateIndex = 0;
	#endregion

	public double SimTime => _currentSimTime;
	public double FixedSimTime => _currentFixedSimTime;
	public double RealTime => _currentRealTime;
	public double DeltaTime => _deltaTime;
	public double FixedDeltaTime => _fixedDeltaTime;
	public bool IsSecondsOnly { get => _isSecondsOnly;  set => _isSecondsOnly = value; }

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
		_currentSimTime = Time.timeAsDouble - _restartedSimTime;
		_currentRealTime = Time.realtimeSinceStartupAsDouble - _restartedRealTime;
		_deltaTime = (double)Time.deltaTime;
	}

	void FixedUpdate()
	{
		_currentFixedSimTime = Time.fixedTimeAsDouble - _restartedFixedSimTime;
		_fixedDeltaTime = (double)Time.fixedDeltaTime;
	}

	void LateUpdate()
	{
		var simTs = TimeSpan.FromSeconds(SimTime);
		var realTs = TimeSpan.FromSeconds(RealTime);
		var diffTs = realTs - simTs;

		switch (_hmsUpdateIndex++)
		{
			case 0:
				_hms.SetSimTime(simTs, _isSecondsOnly);
				break;
			case 1:
				_hms.SetRealTime(realTs, _isSecondsOnly);
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
		if ((_prevSimTime >= SimTime) ||
			(_prevRealTime >= RealTime))
		{
			if ((_prevSimTime > SimTime) ||
				(_prevRealTime > RealTime))
			{
				Debug.LogWarning($"Filter SimTime, Prev:{_prevSimTime} > Current:{SimTime} | RealTime, Prev:{_prevRealTime} > Current:{RealTime}");
			}
		}
		else
		{
			PushDeviceMessage<messages.WorldStatistics>(worldStat);
		}

		_prevSimTime = SimTime;
		_prevRealTime = RealTime;
	}

	protected override void OnReset()
	{
		Debug.Log("[Clock] ResetTime called. Restarting clock to zero.");

		_restartedSimTime = Time.timeAsDouble;
		_restartedFixedSimTime = Time.fixedTimeAsDouble;
		_restartedRealTime = Time.realtimeSinceStartupAsDouble;

		// Immediately reflect the reset so that any thread reading
		// Clock.SimTime sees ~0 right away, not the stale cached value
		// from the last Update() frame.
		_currentSimTime = 0;
		_currentFixedSimTime = 0;
		_currentRealTime = 0;
	}
}