/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
#nullable enable

using UnityEngine;

public static partial class SDF2Unity
{
	public static Color ToUnity(this SDFormat.Math.Color value)
	{
		return new Color(value.R, value.G, value.B, value.A);
	}

	public static Color ToColor(this string value)
	{
		var color = SDFormat.Math.Color.Parse(value);
		return color.ToUnity();
	}

	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Vector3 Position(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 ToUnity(this SDFormat.Math.Vector3d value)
	{
		return Position(value.X, value.Y, value.Z);
	}

	public static Vector3 ToUnity(this cloisim.msgs.Vector3d value)
	{
		return Position(value.X, value.Y, value.Z);
	}

	public static Vector3 Scalar(in double x, in double y, in double z)
	{
		var scalarVector = Position(x, y, z);
		scalarVector.x = Mathf.Abs(scalarVector.x);
		scalarVector.y = Mathf.Abs(scalarVector.y);
		scalarVector.z = Mathf.Abs(scalarVector.z);
		return scalarVector;
	}

	public static Vector2 Scale(in SDFormat.Math.Vector2d value)
	{
		return new Vector2(Mathf.Abs((float)value.X), Mathf.Abs((float)value.Y));
	}

	public static Vector2 Scale(in string value)
	{
		return Scale(SDFormat.Math.Vector2d.Parse(value));
	}

	public static Vector3 Scale(in double x, in double y, in double z)
	{
		return Scalar(x, y, z);
	}

	public static Vector3 Scale(in SDFormat.Math.Vector3d value)
	{
		return Scale(value.X, value.Y, value.Z);
	}

	/// <param name="w">right handed system w</param>
	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Quaternion Rotation(in double w, in double x, in double y, in double z)
	{
		return new Quaternion((float)y, (float)-z, (float)-x, (float)w);
	}

	public static Quaternion ToUnity(this SDFormat.Math.Quaterniond value)
	{
		return Rotation(value.W, value.X, value.Y, value.Z);
	}

	public static Vector3 ToUnityPosition(this SDFormat.Math.Pose3d pose)
	{
		return Position(pose.Position.X, pose.Position.Y, pose.Position.Z);
	}

	public static Quaternion ToUnityRotation(this SDFormat.Math.Pose3d pose)
	{
		return Rotation(pose.Rotation.W, pose.Rotation.X, pose.Rotation.Y, pose.Rotation.Z);
	}

	public static Vector2 Size(in SDFormat.Math.Vector2d value)
	{
		return new Vector2((float)value.X, (float)value.Y);
	}

	public static Vector2 Point(in SDFormat.Math.Vector2d value)
	{
		return new Vector2((float)value.Y, (float)value.X);
	}

	public static float CurveOrientation(in float value)
	{
		return CurveOrientationAngle(value) * Mathf.Rad2Deg;
	}

	public static float CurveOrientationAngle(in float value)
	{
		return -value;
	}
}