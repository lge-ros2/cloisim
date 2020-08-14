/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Runtime.InteropServices;
using UnityEngine;
using messages = gazebo.msgs;

public class DeviceHelper
{
	static Clock clock = null;

	public static string GetModelName(in GameObject targetObject, in bool searchOnlyOneDepth = false)
	{
		try
		{
			var nextObject = targetObject.GetComponentInParent<ModelPlugin>();

			if (searchOnlyOneDepth == false)
			{
				while (!nextObject.transform.parent.Equals(nextObject.transform.root))
				{
					nextObject = nextObject.transform.parent.GetComponentInParent<ModelPlugin>();

					if (nextObject == null)
					{
						return string.Empty;
					}
				}
			}

			return nextObject.name;
		}
		catch
		{
			Debug.LogError("Thee is no parent object model");
			return string.Empty;
		}
	}

	public static string GetPartName(in GameObject targetObject)
	{
		return GetModelName(targetObject, true);
	}

	[DllImport("StdHash")]
	public static extern ulong GetStringHashCode(string value);

	public static void SetCurrentTime(in messages.Time gazeboMsgsTime, in bool useRealTime = false)
	{
		try
		{
			if (gazeboMsgsTime != null)
			{
				if (clock == null)
				{
					var coreObject = GameObject.Find("Core");
					if (coreObject != null)
					{
						clock = coreObject.GetComponent<Clock>();
					}
				}

				var simTime = (clock == null) ? Time.time : clock.GetSimTime();
				var realTime = (clock == null) ? Time.realtimeSinceStartup : clock.GetRealTime();

				var timeNow = (useRealTime) ? realTime : simTime;
				gazeboMsgsTime.Sec = (int)timeNow;
				gazeboMsgsTime.Nsec = (int)((timeNow - (float)gazeboMsgsTime.Sec) * (float)1e+9);
			}
		}
		catch
		{
			Debug.LogError("time message is not initialized yet.");
		}
	}

	public static void SetVector3d(messages.Vector3d vector3d, in Vector3 position)
	{
		if (vector3d == null)
		{
			vector3d = new messages.Vector3d();
		}

		vector3d.X = position.x;
		vector3d.Y = position.z;
		vector3d.Z = position.y;
	}

	public static void SetQuaternion(messages.Quaternion quaternion, in Quaternion rotation)
	{
		if (quaternion == null)
		{
			quaternion = new messages.Quaternion();
		}

		quaternion.X = rotation.x * Mathf.Deg2Rad;
		quaternion.Y = rotation.z * Mathf.Deg2Rad;
		quaternion.Z = rotation.y * Mathf.Deg2Rad;
		quaternion.W = rotation.w * Mathf.Deg2Rad;
	}

	public static Matrix4x4 MakeCustomProjectionMatrix(in float hFov, in float vFov, in float near, in float far)
	{
		// construct custom aspect ratio projection matrix
		// math from https://www.scratchapixel.com/lessons/3d-basic-rendering/perspective-and-orthographic-projection-matrix/opengl-perspective-projection-matrix
		float h = 1.0f / Mathf.Tan(hFov * Mathf.Deg2Rad / 2f);
		float v = 1.0f / Mathf.Tan(vFov * Mathf.Deg2Rad / 2f);
		float a = (far + near) / (near - far);
		float b = (2.0f * far * near / (near - far));

		var projMatrix = new Matrix4x4(
			new Vector4(h, 0, 0, 0),
			new Vector4(0, v, 0, 0),
			new Vector4(0, 0, a, -1),
			new Vector4(0, 0, b, 0));

		return projMatrix;
	}

	public static float HorizontalToVerticalFOV(in float horizontalFOV, in float aspect = 1.0f)
	{
		return Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan((horizontalFOV * Mathf.Deg2Rad) / 2f) / aspect);
	}

	public static bool IsSamePosition(in float A, in float B)
	{
		var distance = Mathf.Abs(A - B);
		if (distance < Mathf.Epsilon)
		{
			return true;
		}
		return false;
	}

	public static bool IsSamePosition(in Vector3 A, in Vector3 B)
	{
		var distance = Vector3.SqrMagnitude(A - B);
		if (distance < Vector3.kEpsilon)
		{
			return true;
		}
		return false;
	}
}