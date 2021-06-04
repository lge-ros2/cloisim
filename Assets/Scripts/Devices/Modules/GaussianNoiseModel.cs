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

	public override void Apply<T>(ref T data, in float deltaTime = 0)
	{
		// Add independent (uncorrelated) Gaussian noise to each input value.
		var whiteNoise = RandomNumberGenerator.GetNormal(parameter.mean, parameter.stddev);

		// Generate varying (correlated) bias for each input value.
		// This implementation is based on the one available in Rotors:
		// https://github.com/ethz-asl/rotors_simulator/blob/master/rotors_gazebo_plugins/src/gazebo_imu_plugin.cpp
		//
		// More information about the parameters and their derivation:
		//
		//  https://github.com/ethz-asl/kalibr/wiki/IMU-Noise-Model
		//
		var bias = this.bias;
		if (parameter.dynamic_bias_stddev > 0 &&
			parameter.dynamic_bias_correlation_time > 0)
		{
			var sigmaB = parameter.dynamic_bias_stddev;
			var tau = parameter.dynamic_bias_correlation_time;

			double sigmaBD = Math.Sqrt(-sigmaB * sigmaB * tau / 2 * Expm1(-2 * deltaTime / tau));

			double phiD = Math.Exp(-deltaTime / tau);
			bias = phiD * bias + RandomNumberGenerator.GetNormal(0, sigmaBD);
		}

		var output = Convert.ToDouble(data) + bias + whiteNoise;

		if (quantized)
		{
			// Apply parameter.precision
			if (Math.Abs(this.parameter.precision - 0d) > Epsilon)
			{
				output = Math.Round(output / parameter.precision) * parameter.precision;
			}
		}

		data = (T)Convert.ChangeType(Clamp(output), typeof(T));
	}
}