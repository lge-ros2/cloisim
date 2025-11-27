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

		public Noise(in SDF.Noise noise)
		{
			switch (noise.type)
			{
				case "gaussian":
				case "gaussian_quantized":
					_noiseModel = new GaussianNoiseModel(noise);
					if (noise.type == "gaussian_quantized")
					{
						_noiseModel.SetQuantization(true);
					}
					break;

				case "custom":
					_noiseModel = new CustomNoiseModel(noise);
					break;

				case "none":
				default:
					_noiseModel = null;
					break;
			}
		}

		public void SetCustomNoiseParameter(in SDF.Plugin plugin)
		{
			if (_noiseModel as CustomNoiseModel != null)
			{
				(_noiseModel as CustomNoiseModel).ParseParameter(plugin);
			}
			// else
			// {
			// 	Console.Write("noise type is not a 'custom'");
			// }
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