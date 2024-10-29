/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private double _pGain, _iGain, _dGain;
	private double _integralError = 0;
	private double _lastError = 0;
	private double _integralMin, _integralMax;
	private double _commandMin, _commandMax;

	public PID(
		in double pGain, in double iGain, in double dGain,
		in double integralMin = -100, in double integralMax = 100,
		in double commandMin = -1000, in double commandMax = 1000)
	{
		Change(pGain, iGain, dGain);
		SetIntegralRange(integralMin, integralMax);
		SetOutputRange(commandMin, commandMax);
	}

	public PID(
		in double pGain, in double iGain, in double dGain,
		in double integralLimit, in double commandLimit)
		: this(
			pGain, iGain, dGain,
			-Math.Abs(integralLimit), Math.Abs(integralLimit),
			-Math.Abs(commandLimit), Math.Abs(commandLimit))
	{
	}

	public void SetIntegralRange(in double min, in double max)
	{
		this._integralMin = min;
		this._integralMax = max;
	}

	public void SetOutputRange(in double min, in double max)
	{
		this._commandMin = min;
		this._commandMax = max;
	}

	public void Change(in double pGain, in double iGain, in double dGain)
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

	public double Update(in double actual, in double target, in double deltaTime)
	{
		var error = actual - target;
		return Update(error, deltaTime);
	}

	public double Update(in double error, in double deltaTime)
	{
		if (Math.Abs(deltaTime) < double.Epsilon ||
			double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
		{
			return 0;
		}

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
		return Math.Clamp(cmd, _commandMin, _commandMax);
	}
}