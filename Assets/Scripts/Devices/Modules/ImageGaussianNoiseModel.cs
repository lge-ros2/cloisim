/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class ImageGaussianNoiseModel : GaussianNoiseModel
{
	public ImageGaussianNoiseModel(in SDF.Noise parameter)
		: base(parameter)
	{
	}

	public override void Apply<T>(ref T data, in float deltaTime = 0)
	{
		// TODO:
	}
}