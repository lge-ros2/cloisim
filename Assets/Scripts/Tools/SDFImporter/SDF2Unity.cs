/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class SDF2Unity
{
	private static string commonShaderName = "Standard (Specular setup)";
	public static Shader commonShader = Shader.Find(commonShaderName);

	public static Vector3 GetPosition(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 GetPosition(in SDF.Vector3<double> value)
	{
		return (value == null)? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Vector3 GetPosition(in SDF.Vector3<int> value)
	{
		return (value == null)? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in SDF.Quaternion<double> value)
	{
		if (value == null)
		{
			return Quaternion.identity;
		}

		var roll = Mathf.Rad2Deg * (float)value.Pitch;
		var pitch = Mathf.Rad2Deg * -(float)value.Yaw;
		var yaw = Mathf.Rad2Deg * -(float)value.Roll;
		return Quaternion.Euler(roll, pitch, yaw);
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

	public static Vector3 GetAxis(SDF.Vector3<int> axisValue, SDF.Quaternion<double> axisRotation = null)
	{
		var pos = GetPosition(axisValue);
		var rot = GetRotation(axisRotation);

		if (!rot.Equals(Quaternion.identity))
		{
			pos = rot * pos;
		}

		return pos;
	}

	public static bool CheckTopModel(in GameObject targetObject)
	{
		return CheckTopModel(targetObject.transform);
	}

	public static bool CheckTopModel(in Transform targetTransform)
	{
		return targetTransform.parent.Equals(targetTransform.root);
	}
}