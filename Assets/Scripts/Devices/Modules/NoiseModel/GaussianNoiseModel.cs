/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public class GaussianNoiseModel : NoiseModel
{
	public GaussianNoiseModel(in SDF.Noise parameter)
		: base(parameter)
	{
	}

	private double ComputeDynamicBias(in float deltaTime)
	{
		var bias = this.bias;
		if (_parameter.dynamic_bias_stddev > 0 && 
			_parameter.dynamic_bias_correlation_time > 0)
		{
			var sigmaB = _parameter.dynamic_bias_stddev;
			var tau = _parameter.dynamic_bias_correlation_time;

			var sigmaBD = Math.Sqrt(-sigmaB * sigmaB * tau / 2 * Expm1(-2 * deltaTime / tau));
			var phiD = Math.Exp(-deltaTime / tau);
			bias = phiD * bias + RandomNumberGenerator.GetNormal(0, sigmaBD);
		}
		return bias;
	}
	
	public override T Generate<T>(T data, float deltaTime)
	{
		// Add independent (uncorrelated) Gaussian noise to each input value.
		var whiteNoise = RandomNumberGenerator.GetNormal(_parameter.mean, _parameter.stddev);

		// Generate varying (correlated) bias for each input value.
		// This implementation is based on the one available in Rotors:
		// https://github.com/ethz-asl/rotors_simulator/blob/master/rotors_gazebo_plugins/src/gazebo_imu_plugin.cpp
		//
		// More information about the parameters and their derivation:
		//
		//  https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model
		//
		var bias = ComputeDynamicBias(deltaTime);

		var output = Convert.ToDouble(data) + bias + whiteNoise;

		if (_quantized)
		{
			// Apply parameter.precision
			if (Math.Abs(_parameter.precision - 0d) > Epsilon)
			{
				output = Math.Round(output / _parameter.precision) * _parameter.precision;
			}
		}

		return (T)Convert.ChangeType(Clamp(output), typeof(T));
	}
}