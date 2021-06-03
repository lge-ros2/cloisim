/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public interface INoiseModel
{
	void Apply(ref float[] data);
}

public class NoiseModel : INoiseModel
{
	public virtual void Apply(ref float[] data)
	{

	}
}

public class CustomNoiseModel : NoiseModel
{
	public override void Apply(ref float[] data)
	{

	}
}