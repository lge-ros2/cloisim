/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class SDF2Unity
{
	public static Vector3 Abs(in Vector3 value)
	{
		return new Vector3(
			Mathf.Abs(value.x),
			Mathf.Abs(value.y),
			Mathf.Abs(value.z));
	}

	public static Color Color(in SDF.Color value)
	{
		return new Color((float)value.R, (float)value.G, (float)value.B, (float)value.A);
	}

	public static Color Color(in string value)
	{
		var color = new SDF.Color(value);
		return Color(color);
	}

	public static Vector3 Scalar(in double x, in double y, in double z)
	{
		var scalarVector = Position(x, y, z);
		scalarVector.x = Mathf.Abs(scalarVector.x);
		scalarVector.y = Mathf.Abs(scalarVector.y);
		scalarVector.z = Mathf.Abs(scalarVector.z);
		return scalarVector;
	}

	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Vector3 Position(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 Position(in Vector3 value)
	{
		return Position(value.x, value.y, value.z);
	}

	public static Vector3 Position(in SDF.Vector3<double> value)
	{
		return (value == null) ? Vector3.zero : Position(value.X, value.Y, value.Z);
	}

	public static Vector3 Position(in SDF.Vector3<int> value)
	{
		return (value == null) ? Vector3.zero : Position(value.X, value.Y, value.Z);
	}

	public static Quaternion Rotation(in SDF.Quaternion<double> value)
	{
		return (value == null) ? Quaternion.identity : Rotation(value.W, value.X, value.Y, value.Z);
	}

	/// <param name="w">right handed system w</param>
	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Quaternion Rotation(in double w, in double x, in double y, in double z)
	{
		return new Quaternion((float)y, (float)-z, (float)-x, (float)w);
	}

	public static Vector2 Scale(in string value)
	{
		return Scale(new SDF.Vector2<double>(value));
	}

	public static Vector2 Scale(in SDF.Vector2<double> value)
	{
		return new Vector2(Mathf.Abs((float)value.X), Mathf.Abs((float)value.Y));
	}

	public static Vector3 Scale(in SDF.Vector3<double> value)
	{
		return Scale(value.X, value.Y, value.Z);
	}

	public static Vector3 Scale(in double x, in double y, in double z)
	{
		var scaleVector = Position(x, y, z);
		scaleVector.x = Mathf.Abs(scaleVector.x);
		scaleVector.y = Mathf.Abs(scaleVector.y);
		scaleVector.z = Mathf.Abs(scaleVector.z);
		return scaleVector;
	}

	public static Vector2 Size(in SDF.Vector2<double> value)
	{
		return new Vector2((float)value.X, (float)value.Y);
	}

	public static Vector2 Point(in SDF.Vector2<double> value)
	{
		return new Vector2((float)value.Y, (float)value.X);
	}

	public static Vector3 Normal(in SDF.Vector3<int> value)
	{
		return Position(value);
	}

	public static Vector3 Axis(SDF.Vector3<int> axis)
	{
		return Position(axis);
	}

	public static Vector3 Direction(SDF.Vector3<double> direction)
	{
		return Position(direction);
	}

	public static float CurveOrientation(in float value)
	{
		return -value * Mathf.Rad2Deg;
	}
}