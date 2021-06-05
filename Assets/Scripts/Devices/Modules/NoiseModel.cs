/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public interface INoiseModel
{
	void Apply<T>(ref T data, in float deltaTime);
	void Apply<T>(ref T[] data, in float deltaTime);
}

public class NoiseModel : INoiseModel
{
	protected readonly SDF.Noise parameter;

	protected double bias = 0;
	protected bool quantized = false;
	protected const double Epsilon = 1e-6d;

	protected double clampMin = double.NegativeInfinity;
	protected double clampMax = double.PositiveInfinity;

	protected NoiseModel(in SDF.Noise parameter)
	{
		this.parameter = parameter;

		if (double.IsNaN(this.parameter.precision))
		{
			if (this.parameter.precision < 0)
			{
				System.Console.WriteLine("Noise precision cannot be less than 0");
			}
			else if (Math.Abs(this.parameter.precision - 0d) > Epsilon)
			{
				this.quantized = true;
			}
		}
		SampleBias();
	}

	public void SetClampMin(in double val)
	{
		this.clampMin = val;
	}

	public void SetClampMax(in double val)
	{
		this.clampMax = val;
	}

	public virtual void Apply<T>(ref T data, in float deltaTime = 0) {}

	public void Apply<T>(ref T[] data, in float deltaTime = 0)
	{
		for (var i = 0; i < data.LongLength; i++)
		{
			Apply<T>(ref data[i], deltaTime);
		}
	}

	private void SampleBias()
	{
		this.bias = RandomNumberGenerator.GetNormal(parameter.bias_mean, parameter.bias_stddev);
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
}
