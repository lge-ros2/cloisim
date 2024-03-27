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

	public static Color GetColor(in SDF.Color value)
	{
		return new Color((float)value.R, (float)value.G, (float)value.B, (float)value.A);
	}

	public static Color GetColor(in string value)
	{
		var color = new SDF.Color(value);
		return GetColor(color);
	}

	public static Vector3 GetScalar(in double x, in double y, in double z)
	{
		return new Vector3(Mathf.Abs((float)y), Mathf.Abs((float)z), Mathf.Abs((float)x));
	}

	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Vector3 GetPosition(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 GetPosition(in Vector3 value)
	{
		return GetPosition(value.x, value.y, value.z);
	}

	public static Vector3 GetPosition(in SDF.Vector3<double> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Vector3 GetPosition(in SDF.Vector3<int> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in double x, in double y, in double z)
	{
		return GetRotation(new SDF.Quaternion<double>(x, y, z));
	}

	public static Quaternion GetRotation(in SDF.Vector3<double> value)
	{
		return GetRotation(new SDF.Quaternion<double>(value.X, value.Y, value.Z));
	}

	public static Quaternion GetRotation(in SDF.Quaternion<double> value)
	{
		return (value == null) ? Quaternion.identity : GetRotation(value.W, value.X, value.Y, value.Z);
	}

	/// <param name="w">right handed system w</param>
	/// <param name="x">right handed system x</param>
	/// <param name="y">right handed system y</param>
	/// <param name="z">right handed system z</param>
	public static Quaternion GetRotation(in double w, in double x, in double y, in double z)
	{
		return new Quaternion((float)y, (float)-z, (float)-x, (float)w);
	}

	public static Vector2 GetScale(in string value)
	{
		return GetScale(new SDF.Vector2<double>(value));
	}

	public static Vector2 GetScale(in SDF.Vector2<double> value)
	{
		return GetScale(new Vector2((float)value.X, (float)value.Y));
	}

	public static Vector2 GetScale(in Vector2 value)
	{
		return new Vector2(Mathf.Abs(value.x), Mathf.Abs(value.y));
	}

	public static Vector3 GetScale(in SDF.Vector3<double> value)
	{
		return GetScale(new Vector3((float)value.X, (float)value.Y, (float)value.Z));
	}

	public static Vector3 GetScale(in Vector3 value)
	{
		var scaleVector = GetPosition(value);
		scaleVector.x = Mathf.Abs(scaleVector.x);
		scaleVector.y = Mathf.Abs(scaleVector.y);
		scaleVector.z = Mathf.Abs(scaleVector.z);
		return scaleVector;
	}

	public static Vector3 GetNormal(in SDF.Vector3<int> value)
	{
		return GetPosition(value);
	}

	public static Vector3 GetAxis(SDF.Vector3<int> axis)
	{
		return GetPosition(axis);
	}

	public static Vector3 GetDirection(SDF.Vector3<double> direction)
	{
		return GetPosition(direction);
	}

	public static float CurveOrientation(in float value)
	{
		return -value * Mathf.Rad2Deg;
	}
}