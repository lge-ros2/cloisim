/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private float pFactor, iFactor, dFactor;
	private float integral = 0;
	private float lastError = 0;

	public PID(in float pFactor, in float iFactor, in float dFactor)
	{
		this.pFactor = pFactor;
		this.iFactor = iFactor;
		this.dFactor = dFactor;
	}

	public PID Copy()
	{
		var newPID = new PID(pFactor, iFactor, dFactor);
		return newPID;
	}

	public void Reset()
	{
		integral = 0;
		lastError = 0;
	}

	public float Update(in float setpoint, in float actual, in float timeFrame)
	{
		float present = Math.Abs(setpoint - actual);
		integral += present * timeFrame;

		float deriv = (present - lastError) / timeFrame;
		lastError = present;

		var cmd = present * pFactor + integral * iFactor + deriv * dFactor;

		return Math.Abs(cmd);
	}
}