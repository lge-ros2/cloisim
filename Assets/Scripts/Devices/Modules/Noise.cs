/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

namespace SensorDevices
{
	public class Noise
	{
		public enum Type
		{
			NONE,
			CUSTOM,
			GAUSSIAN
		};

		private readonly SDF.Noise parameter;

		private Type noiseType;

		public Type NoiseType
		{
			get => noiseType;
			set => noiseType = value;
		}

		private NoiseModel noiseModel = null;

		public Noise(in SDF.Noise noise, in string sensorType)
		{
			this.parameter = noise;

			switch (noise.type)
			{
				case "gaussian":
				case "gaussian_quantized":
					this.noiseType = Type.GAUSSIAN;
					break;

				case "custom":
					this.noiseType = Type.CUSTOM;
					break;

				case "none":
				default:
					this.noiseType = Type.NONE;
					break;
			}

			SetNoiseModel(sensorType);
		}

		public void SetNoiseModel(in string sensorType)
		{
			// Check for 'gaussian' noise. The 'gaussian_quantized' type is kept for
			// backward compatibility.
			switch (NoiseType)
			{
				case Type.GAUSSIAN:

					switch (sensorType)
					{
						case "camera":
						case "depth":
						case "depth_camera":
						case "rgbd":
						case "rgbd_camera":
						case "multicamera":
						case "wideanglecamera":
							noiseModel = new ImageGaussianNoiseModel(this.parameter);
							break;
						default:
							noiseModel = new GaussianNoiseModel(this.parameter);
							break;
					}
					break;

				case Type.CUSTOM:
					// Return empty noise if 'none' or 'custom' is specified.
					noiseModel = new CustomNoiseModel(this.parameter);
					break;

				case Type.NONE:
					noiseModel = null;
					break;

				default:
					System.Console.WriteLine("Unrecognized noise type: " + noiseType);
					break;
			}
		}

		public void SetClampMin(in double val)
		{
			if (noiseModel != null)
			{
				noiseModel.SetClampMin(val);
			}
		}

		public void SetClampMax(in double val)
		{
			if (noiseModel != null)
			{
				noiseModel.SetClampMax(val);
			}
		}

		public void Apply<T>(ref T data, in float deltaTime = 0)
		{
			if (noiseModel != null)
			{
				switch (noiseType)
				{
					case Type.GAUSSIAN:
						noiseModel.Apply<T>(ref data, deltaTime);
						break;

					case Type.CUSTOM:
						noiseModel.Apply<T>(ref data, deltaTime);
						break;

					case Type.NONE:
					default:
						break;
				}
			}
		}

		public void Apply<T>(ref T[] dataArray, in float deltaTime = 0)
		{
			if (noiseModel != null)
			{
			switch (noiseType)
				{
					case Type.GAUSSIAN:
						noiseModel.Apply<T>(ref dataArray, deltaTime);
						break;

					case Type.CUSTOM:
						noiseModel.Apply<T>(ref dataArray, deltaTime);
						break;

					case Type.NONE:
					default:
						break;
				}
			}
		}
	}
}