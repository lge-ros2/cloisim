/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class Noise
{
	public enum Type
	{
		NONE,
		CUSTOM,
		GAUSSIAN
	};

	private Type noiseType;
	private string sensorType;

	private NoiseModel noiseModel = null;

	public Type NoiseType
	{
		get => noiseType;
		set => noiseType = value;
	}

	public Noise(in string noiseType, in string sensorType)
	{
		SetNoiseModel(noiseType, sensorType);
	}

	public void SetNoiseModel(in string noiseType, in string sensorType)
	{
		// Check for 'gaussian' noise. The 'gaussian_quantized' type is kept for
		// backward compatibility.
		switch (noiseType)
		{
			case "gaussian":
			case "gaussian_quantized":

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
				// GZ_ASSERT(noise->GetNoiseType() == Noise::GAUSSIAN,
				// 	"Noise type should be 'gaussian'");

				break;

			case "none":
			case "custom":
				// Return empty noise if 'none' or 'custom' is specified.
				// if 'custom', the type will be set once the user calls the
				// SetCustomNoiseCallback function.
				noiseModel = new CustomNoiseModel();
				// noise.reset(new Noise(Noise::NONE));
				// GZ_ASSERT(noise->GetNoiseType() == Noise::NONE,
				// 	"Noise type should be 'none'");
				break;

			default:
				System.Console.WriteLine("Unrecognized noise type: " + noiseType);
				break;
		}
	}

	public void Apply(ref float[] dataArray)
	{
		// if (this->type == NONE)
		// 	return _in;
		// else if (this->type == CUSTOM)
		// {
		// 	if (this->customNoiseCallback)
		// 		return this->customNoiseCallback(_in);
		// 	else
		// 	{
		// 		gzerr << "Custom noise callback function not set!"
		// 			<< " Please call SetCustomNoiseCallback within a sensor plugin."
		// 			<< std::endl;
		// 		return _in;
		// 	}
		// }
		// else
		// 	return this->ApplyImpl(_in);
	}
}