/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private float pGain_, iGain_, dGain_;
	private float integral = 0;
	private float lastError = 0;
	private float outputMax = 1000f;

	public PID(in float pGain, in float iGain, in float dGain)
	{
		Change(pGain, iGain, dGain);
	}

	public PID Copy()
	{
		var newPID = new PID(pGain_, iGain_, dGain_);
		return newPID;
	}

	public void Change(in float pGain, in float iGain, in float dGain)
	{
		this.pGain_ = pGain;
		this.iGain_ = iGain;
		this.dGain_ = dGain;
	}

	public void Reset()
	{
		integral = 0;
		lastError = 0;
	}

	public float Update(in float setpoint, in float actual, in float deltaTime)
	{
		var error = setpoint - actual;

		integral += error * deltaTime;

		var derive = (deltaTime == 0f)? 0f : ((error - lastError) / deltaTime);

		lastError = error;

		var output = error * pGain_ + integral * iGain_ + derive * dGain_;

		return (output > outputMax)? Math.Abs(outputMax): Math.Abs(output);
	}
}