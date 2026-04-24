/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public abstract class NoiseModel
{
	protected readonly SDFormat.Noise _parameter;

	protected double bias = 0;
	protected bool _quantized = false;
	protected const double Epsilon = 1e-6d;

	protected double clampMin = double.NegativeInfinity;
	protected double clampMax = double.PositiveInfinity;

	protected NoiseModel(in SDFormat.Noise parameter)
	{
		_parameter = parameter;

		if (double.IsNaN(_parameter.Precision))
		{
			if (_parameter.Precision < 0)
			{
				Console.WriteLine("Noise precision cannot be less than 0");
			}
			else if (Math.Abs(_parameter.Precision - 0d) > Epsilon)
			{
				_quantized = true;
				Console.WriteLine($"Noise Quantized, precision={_parameter.Precision}");
			}
		}
		SampleBias();
	}

	public void SetQuantization(in bool val)
	{
		_quantized = val;
	}

	public void SetClampMin(in double val)
	{
		clampMin = val;
	}

	public void SetClampMax(in double val)
	{
		clampMax = val;
	}

	private void SampleBias()
	{
		bias = RandomNumberGenerator.GetNormal(_parameter.BiasMean, _parameter.BiasStdDev);
		// With equal probability, we pick a negative bias
		// (by convention, rateBiasMean should be positive, though it would work fine if negative).

		if (RandomNumberGenerator.GetUniform() < 0.5d)
		{
			bias = -bias;
		}
	}

	protected double Expm1(in double value)
	{
		return Math.Exp(value) - 1;
	}

	protected double Clamp(in double value)
	{
		return (clampMax != double.PositiveInfinity && value > clampMax) ?
			clampMax : ((clampMin != double.NegativeInfinity && value < clampMin) ? clampMin : value);
	}

	public abstract T Generate<T>(T data, float deltaTime);
}
