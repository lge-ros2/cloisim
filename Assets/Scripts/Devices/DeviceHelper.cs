/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Runtime.InteropServices;
using UnityEngine;
using messages = gazebo.msgs;

public partial class DeviceHelper
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

		vector3d.X = position.z;
		vector3d.Y = -position.x;
		vector3d.Z = position.y;
	}

	public static void SetQuaternion(messages.Quaternion quaternion, in Quaternion rotation)
	{
		if (quaternion == null)
		{
			quaternion = new messages.Quaternion();
		}

		quaternion.X = rotation.w;
		quaternion.Y = rotation.x;
		quaternion.Z = -rotation.y;
		quaternion.W = rotation.z;
	}

	public static bool IsSamePosition(in float A, in float B)
	{
		return (Mathf.Abs(A - B) <= Mathf.Epsilon) ? true : false;
	}

	public static bool IsSamePosition(in Vector3 A, in Vector3 B)
	{
		return (Vector3.SqrMagnitude(A - B) <= Vector3.kEpsilon) ? true : false;
	}
}