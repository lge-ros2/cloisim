/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
#nullable enable

using UnityEngine;
using System;

public static class Unity2SDF
{
	/// <summary>
	/// Convert to right handed coordinates
	/// </summary>
	///

	public static Vector3 AsUnity(this SDF.Vector3<double>? value)
	{
		return (value == null) ? Vector3.zero : new Vector3((float)value.X, (float)value.Y, (float)value.Z);
	}

	public static SDF.Vector3<double> Vector(in Vector3 value)
	{
		return new SDF.Vector3<double>(value.z, -value.x, value.y);
	}

	public static SDF.Vector3<double> Scale(in Vector3 value)
	{
		var scale = Vector(value);
		scale.X = Math.Abs(scale.X);
		scale.Y = Math.Abs(scale.Y);
		scale.Z = Math.Abs(scale.Z);
		return scale;
	}

	public static SDF.Vector3<double> Position(in Vector3 value)
	{
		return Vector(value);
	}

	public static SDF.Quaternion<double> Rotation(in Quaternion value)
	{
		return new SDF.Quaternion<double>(value.w, -value.z, value.x, -value.y);
	}

	public static SDF.Pose<double> Pose(in SDF.Vector3<double> position, in SDF.Quaternion<double> rotation)
	{
		return new SDF.Pose<double>(position, rotation);
	}

	public class Direction
	{
		public static Vector3 Reverse(in Vector3 value)
		{
			return -value;
		}

		public static float Curve(in float value)
		{
			return -value;
		}

		public static double Curve(in double value)
		{
			return -value;
		}

		public class Joint
		{
			public static float Prismatic(in float value, in Vector3 rotation)
			{
				return (Mathf.Approximately(rotation.x, 180) ||
						Mathf.Approximately(rotation.y, 180) ||
						Mathf.Approximately(rotation.z, 180)) ? -value : value;
			}
		}
	}
}