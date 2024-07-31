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
	private float _integralMin, _integralMax;
	private float _cmdMin, _cmdMax;

	public PID(
		in float pGain, in float iGain, in float dGain,
		in float integralMin = -100, in float integralMax = 100,
		in float cmdMin = -1000, in float cmdMax = 1000)
	{
		Change(pGain, iGain, dGain);
		SetIntegralRange(integralMin, integralMax);
		SetOutputRange(cmdMin, cmdMax);
	}

	public void SetIntegralRange(in float min, in float max)
	{
		this._integralMin = min;
		this._integralMax = max;
	}

	public void SetOutputRange(in float min, in float max)
	{
		this._cmdMin = min;
		this._cmdMax = max;
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
			// UnityEngine.Debug.Log("MAX=" + iTerm);
			_integralError = iTerm / _iGain;
		}
		else if (iTerm < _integralMin)
		{
			iTerm = _integralMin;
			// UnityEngine.Debug.Log("MIN=" + iTerm);
			_integralError = iTerm / _iGain;
		}

		// Calculate the derivative error
		var dErr = (error - _lastError) / deltaTime;
		_lastError = error;

		// Calculate derivative contribution to command
		var dTerm = _dGain * dErr;

		var cmd = -pTerm - iTerm - dTerm;

		// UnityEngine.Debug.Log("cmd=" + cmd + " cmdMin=" + _cmdMin + " cmdMax=" + _cmdMax);
		return UnityEngine.Mathf.Clamp(cmd, _cmdMin, _cmdMax);
	}
}