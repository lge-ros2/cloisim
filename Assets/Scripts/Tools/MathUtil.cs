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

		public static UnityEngine.Vector3 GetEuler(in UnityEngine.Quaternion rotation)
		{
			var euler = rotation.eulerAngles;
			var angleX = Math.Atan2(2f * (rotation.w * rotation.x + rotation.y * rotation.z),
									1f - 2f * (rotation.x * rotation.x + rotation.y * rotation.y));

			angleX = Normalize(angleX);
			angleX *= UnityEngine.Mathf.Rad2Deg;
			euler.x = (float)angleX;

			return euler;
		}
	}

	public static void NormalizeAngle(this ref double angle)
	{
		angle = Angle.Normalize(angle);
	}

	public static void NormalizeAngle(this ref float angle)
	{
		angle = (float)Angle.Normalize((double)angle);
	}
}