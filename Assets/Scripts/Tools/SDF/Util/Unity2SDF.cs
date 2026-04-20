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

	public static Vector3 AsUnity(this SDFormat.Math.Vector3d value)
	{
		return new Vector3((float)value.X, (float)value.Y, (float)value.Z);
	}

	public static SDFormat.Math.Vector3d Vector(in Vector3 value)
	{
		return new SDFormat.Math.Vector3d(value.z, -value.x, value.y);
	}

	public static SDFormat.Math.Vector3d Scale(in Vector3 value)
	{
		var scale = Vector(value);
		return new SDFormat.Math.Vector3d(Math.Abs(scale.X), Math.Abs(scale.Y), Math.Abs(scale.Z));
	}

	public static SDFormat.Math.Vector3d Position(in Vector3 value)
	{
		return Vector(value);
	}

	public static SDFormat.Math.Quaterniond Rotation(in Quaternion value)
	{
		return new SDFormat.Math.Quaterniond(value.w, -value.z, value.x, -value.y);
	}

	public static SDFormat.Math.Pose3d Pose(in Vector3 position, in Quaternion rotation)
	{
		return new SDFormat.Math.Pose3d(Position(position), Rotation(rotation));
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