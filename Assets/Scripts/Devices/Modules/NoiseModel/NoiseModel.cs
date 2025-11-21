/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public abstract class NoiseModel
{
	protected readonly SDF.Noise _parameter;

	protected double bias = 0;
	protected bool _quantized = false;
	protected const double Epsilon = 1e-6d;

	protected double clampMin = double.NegativeInfinity;
	protected double clampMax = double.PositiveInfinity;

	protected NoiseModel(in SDF.Noise parameter)
	{
		this._parameter = parameter;

		if (double.IsNaN(_parameter.precision))
		{
			if (_parameter.precision < 0)
			{
				System.Console.WriteLine("Noise precision cannot be less than 0");
			}
			else if (Math.Abs(_parameter.precision - 0d) > Epsilon)
			{
				this._quantized = true;
				System.Console.WriteLine($"Noise Quantized, precision={_parameter.precision}");
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
		this.clampMin = val;
	}

	public void SetClampMax(in double val)
	{
		this.clampMax = val;
	}

	private void SampleBias()
	{
		this.bias = RandomNumberGenerator.GetNormal(_parameter.bias_mean, _parameter.bias_stddev);
		// With equal probability, we pick a negative bias
		// (by convention, rateBiasMean should be positive, though it would work fine if negative).

		if (RandomNumberGenerator.GetUniform() < 0.5d)
		{
			this.bias = -this.bias;
		}
	}

	protected double Expm1(in double value)
	{
		return System.Math.Exp(value) - 1;
	}

	protected double Clamp(in double value)
	{
		return (clampMax != double.PositiveInfinity && value > clampMax) ?
			this.clampMax : ((clampMin != double.NegativeInfinity && value < clampMin) ? clampMin : value);
	}

	public abstract T Generate<T>(T data, float deltaTime);
}
