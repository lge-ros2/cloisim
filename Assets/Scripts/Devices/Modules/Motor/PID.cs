/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

[Serializable]
public class PID
{
	private const double IntegralMax = 100;
	private const double CommandMax = 1000;

	private double _pGain, _iGain, _dGain;
	private double _integralError = 0;
	private double _lastError = 0;
	private double _integralMin, _integralMax;
	private double _commandMin, _commandMax;

	public double PGain => _pGain;
	public double IGain => _iGain;
	public double DGain => _dGain;
	public double IntegralRangeMin => _integralMin;
	public double IntegralRangeMax => _integralMax;
	public double OutputRangeMin => _commandMin;
	public double OutputRangeMax => _commandMax;

	public PID(
		double pGain, double iGain, double dGain,
		double integralMin, double integralMax,
		double commandMin, double commandMax)
	{
		Change(pGain, iGain, dGain);

		SetIntegralRange(
			double.IsNegativeInfinity(integralMin)? -IntegralMax: integralMin,
			double.IsPositiveInfinity(integralMax)? IntegralMax: integralMax);

		SetOutputRange(
			double.IsNegativeInfinity(commandMin)? -CommandMax: commandMin,
			double.IsPositiveInfinity(commandMax)? CommandMax: commandMax);
	}

	public PID(
		double pGain, double iGain, double dGain,
		double integralLimit = IntegralMax, double commandLimit = CommandMax)
		: this(
			pGain, iGain, dGain,
			-Math.Abs(integralLimit), Math.Abs(integralLimit),
			-Math.Abs(commandLimit), Math.Abs(commandLimit))
	{
	}

	public void SetIntegralRange(double min, double max)
	{
		_integralMin = Math.Min(min, max);
		_integralMax = Math.Max(min, max);
	}

	public void SetOutputRange(double min, double max)
	{
		_commandMin = Math.Min(min, max);
		_commandMax = Math.Max(min, max);
	}

	public void Change(double pGain, double iGain, double dGain)
	{
		_pGain = pGain;
		_iGain = iGain;
		_dGain = dGain;
	}

	public void Reset()
	{
		_integralError = 0;
		_lastError = 0;
	}

	public double Update(double actual, double target, double deltaTime)
	{
		var error = actual - target;
		return Update(error, deltaTime);
	}

	public double Update(double error, double deltaTime)
	{
		if (deltaTime <= 0d ||
			double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
		{
			return 0;
		}

		// Calculate proportional contribution to command
		var pTerm = _pGain * error;

		var iTerm = 0.0;
		if (_iGain != 0.0)
		{
			// Calculate the integral error
			_integralError += deltaTime * error;

			// Calculate integral contribution to command
			iTerm = _iGain * _integralError;

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
		}
		else
		{
			_integralError = 0;
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