/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public static class MathUtil
{
	private const double PI = Math.PI;
	private const double PI2 = PI * 2;

	public readonly struct MinMax
	{
		public readonly float min;
		public readonly float max;
		public readonly float range;

		public MinMax(in float min = 0, in float max = 0)
		{
			this.min = min;
			this.max = max;
			this.range = max - min;
		}

		public MinMax(in double min = 0, in double max = 0)
		{
			this.min = (float)min;
			this.max = (float)max;
			this.range = (float)(max - min);
		}

		public override string ToString()
		{
			return $"MinMax[ min:{min}, max:{max}, range:{range} ]";
		}
	}

	public static class Angle
	{
		// in Radian
		public static double Normalize(in double angle)
		{
			var normalizedAngle = angle % (2 * PI); // Normalize angle to [0, 2π]
			if (normalizedAngle > PI)
			{
				normalizedAngle -= PI2; // Shift to [-π, π]
			}
			// UnityEngine.Debug.Log("normalize :" +  angle.ToString("F5") + " -> " + normalizedAngle.ToString("F5"));
			return normalizedAngle;
		}
	}

	public static void NormalizeAngle(this ref double angle)
	{
		angle = Angle.Normalize(angle);
	}

	public static void NormalizeAngle(this ref UnityEngine.Vector3 vector)
	{
		vector.x = (float)Angle.Normalize(vector.x);
		vector.y = (float)Angle.Normalize(vector.y);
		vector.z = (float)Angle.Normalize(vector.z);
	}

	public static bool IsZero(in double value)
	{
		return Math.Abs(value) < float.Epsilon;
	}
}