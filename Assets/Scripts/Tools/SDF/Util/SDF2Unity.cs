/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class SDF2Unity
{
	private static readonly string commonShaderName = "Standard (Specular setup)";
	public static Shader commonShader = Shader.Find(commonShaderName);

	public static Color GetColor(in SDF.Color value)
	{
		return new Color((float)value.R, (float)value.G, (float)value.B, (float)value.A);
	}

	public static Vector3 GetScalar(in double x, in double y, in double z)
	{
		return new Vector3(Mathf.Abs((float)y), Mathf.Abs((float)z), Mathf.Abs((float)x));
	}

	public static Vector3 GetPosition(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 GetPosition(in SDF.Vector3<double> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Vector3 GetPosition(in SDF.Vector3<int> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in SDF.Vector3<double> value)
	{
		return GetRotation(new SDF.Quaternion<double>(value.X, value.Y, value.Z));
	}

	public static Quaternion GetRotation(in SDF.Quaternion<double> value)
	{
		return (value == null) ? Quaternion.identity : GetRotation(value.W, value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in double w, in double x, in double y, in double z)
	{
		return new Quaternion((float)y, (float)-z, (float)-x, (float)w);
	}

	public static Vector3 GetScale(in SDF.Vector3<double> value)
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

	public static bool IsTopModel(in GameObject targetObject)
	{
		return IsTopModel(targetObject.transform);
	}

	public static bool IsTopModel(in Transform targetTransform)
	{
		return targetTransform.parent.Equals(targetTransform.root);
	}
}
