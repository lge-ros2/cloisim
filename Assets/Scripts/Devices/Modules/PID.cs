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
	private float _integralError = 0;
	private float _lastError = 0;
	private float _integralMax, _integralMin;
	private float _cmdMax, _cmdMin;

	public PID(
		in float pGain, in float iGain, in float dGain,
		in float integralMax = 100, in float integralMin = -100,
		in float cmdMax = 1000, in float cmdMin = -1000)
	{
		Change(pGain, iGain, dGain);
		this._integralMax = integralMax;
		this._integralMin = integralMin;
		this._cmdMax = cmdMax;
		this._cmdMin = cmdMin;
	}

	public void Change(in float pGain, in float iGain, in float dGain)
	{
		this._pGain = pGain;
		this._iGain = iGain;
		this._dGain = dGain;
	}

	public void Reset()
	{
		_integralError = 0;
		_lastError = 0;
	}

	public float Update(in float target, in float actual, in float deltaTime)
	{
		if (UnityEngine.Mathf.Abs(deltaTime) < float.Epsilon ||
			float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
			return 0f;

		var error = actual - target;

		// Calculate proportional contribution to command
		var pTerm = _pGain * error;

		// Calculate the integral error
		_integralError += deltaTime * error;

		// Calculate integral contribution to command
		var iTerm = _iGain * _integralError;

		// Limit iTerm so that the limit is meaningful in the output
		if (iTerm > _integralMax)
		{
			iTerm = _integralMax;
			_integralError = iTerm / _iGain;
		}
		else if (iTerm < _integralMin)
		{
			iTerm = _integralMin;
			_integralError = iTerm / _iGain;
		}

		// Calculate the derivative error
		var dErr = (error - _lastError) / deltaTime;
		_lastError = error;

		// Calculate derivative contribution to command
		var dTerm = _dGain * dErr;

		var cmd = -pTerm - iTerm - dTerm;

		return UnityEngine.Mathf.Clamp(cmd, _cmdMin, _cmdMax);
	}
}