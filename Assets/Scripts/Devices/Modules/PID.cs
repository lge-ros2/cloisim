/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private float _pGain, _iGain, _dGain;
	private float integral = 0;
	private float lastError = 0;
	private float _outputMax;
	private float _outputMin;

	public PID(in float pGain, in float iGain, in float dGain, in float outputMax = 1000, in float outputMin = -1000)
	{
		Change(pGain, iGain, dGain);
		this._outputMax = outputMax;
		this._outputMin = outputMin;
	}

	public void Change(in float pGain, in float iGain, in float dGain)
	{
		this._pGain = pGain;
		this._iGain = iGain;
		this._dGain = dGain;
	}

	public void Reset()
	{
		integral = 0;
		lastError = 0;
	}

	public float Update(in float target, in float actual, in float deltaTime)
	{
		var error = actual - target;

		integral += (error * deltaTime);

		var derive = (deltaTime == 0)? 0 : ((error - lastError) / deltaTime);

		lastError = error;

		var output = (error * _pGain) + (integral * _iGain) + (derive * _dGain);

		return UnityEngine.Mathf.Clamp(output, _outputMin, _outputMax);
	}
}