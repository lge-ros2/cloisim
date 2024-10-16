/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public struct WheelInfo
{
	public float wheelRadius;
	public float wheelSeparation; // wheel separation
	public readonly float inversedWheelRadius; // for computational performance
	public readonly float inversedWheelSeparation;  // for computational performance

	public WheelInfo(in float radius = 0.1f, in float separation = 0)
	{
		this.wheelRadius = radius;
		this.wheelSeparation = separation;
		this.inversedWheelRadius = 1.0f / wheelRadius;
		this.inversedWheelSeparation = 1.0f / wheelSeparation;
	}
}