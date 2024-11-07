/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SelfBalanceControl
{
	public struct ExpProfiler
	{
		private double _timeConstant;
		private double _initValue;
		private double _offset;
		private double _initTime;

		public ExpProfiler(in double tc)
		{
			this._timeConstant = tc;
			this._initValue = 0;
			this._offset = 0;
			this._initTime = 0;
		}

		public void Reset(in double initTime, in double initialValue, in double offset = 0)
		{
			this._initTime = initTime;
			this._initValue = initialValue;
			this._offset = offset;
			// UnityEngine.Debug.Log("ExpProfiler reset: " + this._initValue);
		}

		public double Generate(in double timeStamp)
		{
			// UnityEngine.Debug.Log("ExpProfiler: Generate=" + this._initValue.ToString("F5") +
			// 	", exp=" + (Math.Exp(-_timeConstant * (timeStamp - _initTime)) +
			// 	" timestamp=" + timeStamp.ToString("F5") + " initTime=" + _initTime.ToString("F5")));
			return _initValue * Math.Exp(-_timeConstant * (timeStamp - _initTime)) + _offset;
		}
	}
}
