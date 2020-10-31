/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private float pFactor_, iFactor_, dFactor_;
	private float integral = 0;
	private float lastError = 0;

	public PID(in float pFactor, in float iFactor, in float dFactor)
	{
		Change(pFactor, iFactor, dFactor);
	}

	public PID Copy()
	{
		var newPID = new PID(pFactor_, iFactor_, dFactor_);
		return newPID;
	}

	public void Change(in float pFactor, in float iFactor, in float dFactor)
	{
		this.pFactor_ = pFactor;
		this.iFactor_ = iFactor;
		this.dFactor_ = dFactor;
	}

	public void Reset()
	{
		integral = 0;
		lastError = 0;
	}

	public float Update(in float setpoint, in float actual, in float timeFrame)
	{
		var present = Math.Abs(setpoint - actual);
		integral += present * timeFrame;

		var derive = (present - lastError) / timeFrame;
		lastError = present;

		var cmd = present * pFactor_ + integral * iFactor_ + derive * dFactor_;
		return Math.Abs(cmd);
	}
}