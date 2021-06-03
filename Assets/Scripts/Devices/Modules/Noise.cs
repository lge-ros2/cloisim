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

		private readonly SDF.Noise noise;

		private Type noiseType;

		public Type NoiseType
		{
			get => noiseType;
			set => noiseType = value;
		}

		private NoiseModel noiseModel = null;

		public Noise(in SDF.Noise noise, in string sensorType)
		{
			this.noise = noise;

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
						case "multicamera":
						case "wideanglecamera":
							noiseModel = new ImageGaussianNoiseModel();
							break;
						default:
							noiseModel = new GaussianNoiseModel();
							break;
					}

					break;

				case Type.CUSTOM:
					// Return empty noise if 'none' or 'custom' is specified.
					// if 'custom', the type will be set once the user calls the SetCustomNoiseCallback function.
					noiseModel = new CustomNoiseModel();
					break;

				case Type.NONE:
					noiseModel =  null;
					break;

				default:
					System.Console.WriteLine("Unrecognized noise type: " + noiseType);
					break;
			}
		}

		public void Apply(ref float[] dataArray)
		{
			switch (noiseType)
			{
				case Type.NONE:

					break;

				case Type.GAUSSIAN:

					break;

				case Type.CUSTOM:

					break;

				default:
					break;
			}
			// if (this->type == NONE)
			// 	return _in;
			// else if (this->type == CUSTOM)
			// {
			// 	if (this->customNoiseCallback)
			// 		return this->customNoiseCallback(_in);
			// 	else
			// 	{
			// 		gzerr << "Custom noise callback function not set!" << " Please call SetCustomNoiseCallback within a sensor plugin." << std::endl;
			// 		return _in;
			// 	}
			// }
			// else
			// 	return this->ApplyImpl(_in);
		}
	}
}