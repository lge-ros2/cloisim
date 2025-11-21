/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class CustomNoiseModel : NoiseModel
{
	public CustomNoiseModel(in SDF.Noise parameter)
		: base(parameter)
	{
	}

	public override T Generate<T>(T data, float deltaTime)
	{
		// TODO: TBD
		return data;
	}
}