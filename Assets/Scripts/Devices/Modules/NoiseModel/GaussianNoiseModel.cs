/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public class GaussianNoiseModel : NoiseModel
{
	public GaussianNoiseModel(in SDFormat.Noise parameter)
		: base(parameter)
	{
	}

	protected double ComputeDynamicBias(in float deltaTime)
	{
		var bias = this.bias;
		if (_parameter.DynamicBiasStdDev > 0 &&
			_parameter.DynamicBiasCorrelationTime > 0)
		{
			var sigmaB = _parameter.DynamicBiasStdDev;
			var tau = _parameter.DynamicBiasCorrelationTime;

			var sigmaBD = Math.Sqrt(-sigmaB * sigmaB * tau / 2 * Expm1(-2 * deltaTime / tau));
			var phiD = Math.Exp(-deltaTime / tau);
			bias = phiD * bias + RandomNumberGenerator.GetNormal(0, sigmaBD);
		}
		return bias;
	}

	public override T Generate<T>(T data, float deltaTime)
	{
		var whiteNoise = RandomNumberGenerator.GetNormal(_parameter.Mean, _parameter.StdDev);

		var bias = ComputeDynamicBias(deltaTime);

		var output = Convert.ToDouble(data) + bias + whiteNoise;

		if (_quantized)
		{
			if (Math.Abs(_parameter.Precision - 0d) > Epsilon)
			{
				output = Math.Round(output / _parameter.Precision) * _parameter.Precision;
			}
		}

		return (T)Convert.ChangeType(Clamp(output), typeof(T));
	}
}