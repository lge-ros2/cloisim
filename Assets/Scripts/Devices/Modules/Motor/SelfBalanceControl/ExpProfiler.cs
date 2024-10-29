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

		public void Reset(in double initTime, in double initialValue, in double offset)
		{
			this._initValue = initialValue;
			this._offset = offset;
			this._initTime = initTime;
		}

		public double Generate(in double timeStamp)
		{
			return _initValue * Math.Exp(-_timeConstant * (timeStamp - _initTime)) + _offset;
		}
	}
}
