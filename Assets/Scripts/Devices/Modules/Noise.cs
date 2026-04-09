/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System;
using System.Threading.Tasks;

namespace SensorDevices
{
	public class Noise
	{
		private ParallelOptions _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = (Environment.ProcessorCount < 4) ? 1 : (Environment.ProcessorCount / 4) };

		private NoiseModel _noiseModel = null;

		public Noise(in SDFormat.Noise noise)
		{
			switch (noise.Type)
			{
				case SDFormat.NoiseType.Gaussian:
				case SDFormat.NoiseType.GaussianQuantized:
					_noiseModel = new GaussianNoiseModel(noise);
					if (noise.Type == SDFormat.NoiseType.GaussianQuantized)
					{
						_noiseModel.SetQuantization(true);
					}
					break;

				default:
					_noiseModel = null;
					break;
			}
		}

		public void SetCustomNoiseParameter(in string customNoiseParamInRawXml)
		{
			var customNoiseModel = _noiseModel as CustomNoiseModel;
			customNoiseModel?.ParseParameter(customNoiseParamInRawXml);
		}

		public void SetClampMin(in double val)
		{
			if (_noiseModel != null)
			{
				_noiseModel.SetClampMin(val);
			}
		}

		public void SetClampMax(in double val)
		{
			if (_noiseModel != null)
			{
				_noiseModel.SetClampMax(val);
			}
		}

		public void Apply<T>(ref T data, in float deltaTime = 0)
		{
			if (_noiseModel != null)
			{
				data = _noiseModel.Generate<T>(data, deltaTime);
			}
		}

		public void Apply<T>(T[] data, float deltaTime = 0)
		{
			if (_noiseModel != null)
			{
				Parallel.For(0, data.Length, _parallelOptions, i =>
				{
					data[i] = _noiseModel.Generate(data[i], deltaTime);
				});
			}
		}
	}
}