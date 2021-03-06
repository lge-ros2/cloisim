/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Runtime.InteropServices;
using UnityEngine;
using messages = cloisim.msgs;

public partial class DeviceHelper
{
	private static Clock clock = null;

	public static Clock GetGlobalClock()
	{
		if (clock == null)
		{
			var coreObject = GameObject.Find("Core");
			clock = coreObject?.GetComponent<Clock>();

			if (clock == null)
			{
					clock = coreObject.AddComponent<Clock>();
			}
		}

		return clock;
	}

	public static string GetModelName(in GameObject targetObject, in bool searchOnlyOneDepth = false)
	{
		try
		{
			var nextObject = targetObject.GetComponentInParent<SDF.Helper.Model>();

			if (searchOnlyOneDepth == false)
			{
				while (!nextObject.transform.parent.Equals(nextObject.transform.root))
				{
					nextObject = nextObject.transform.parent.GetComponentInParent<SDF.Helper.Model>();

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

	public static void SetCurrentTime(messages.Time msgTime, in bool useRealTime = false)
	{
		if (msgTime == null)
		{
			msgTime = new messages.Time();
		}

		var timeNow = (useRealTime) ? GetGlobalClock().GetRealTime() : GetGlobalClock().GetSimTime();
		msgTime.Sec = (int)timeNow;
		msgTime.Nsec = (int)((timeNow - (float)msgTime.Sec) * (float)1e+9);
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

		quaternion.X = -rotation.z;
		quaternion.Y = -rotation.x;
		quaternion.Z = rotation.y;
		quaternion.W = -rotation.w;
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