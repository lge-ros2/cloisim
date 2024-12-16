/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using messages = cloisim.msgs;

public static partial class DeviceHelper
{
	private static Clock globalClock = null;

	private static SphericalCoordinates globalSphericalCoordinates = null;

	public static void SetGlobalClock(in Clock clock)
	{
		globalClock = clock;
	}

	public static Clock GetGlobalClock()
	{
		return globalClock;
	}

	public static Clock GlobalClock => globalClock;

	public static void SetGlobalSphericalCoordinates(in SphericalCoordinates sphericalCoordinates)
	{
		globalSphericalCoordinates = sphericalCoordinates;
	}

	public static SphericalCoordinates GetGlobalSphericalCoordinates()
	{
		return globalSphericalCoordinates;
	}

	public static string GetModelName(in GameObject targetObject)
	{
		try
		{
			SDF.Helper.Base nextObject = targetObject.GetComponentInParent<SDF.Helper.Model>();

			if (nextObject == null)
			{
				nextObject = targetObject.GetComponentInParent<SDF.Helper.Actor>();
			}

			if (nextObject != null && !nextObject.CompareTag("Actor"))
			{
				while (!SDF2Unity.IsRootModel(nextObject.transform))
				{
					nextObject = nextObject.transform.parent.GetComponentInParent<SDF.Helper.Base>();

					if (nextObject == null)
					{
						break;
					}
				}
			}

			if (nextObject == null)
			{
				return string.Empty;
			}

			return nextObject.name;
		}
		catch
		{
			Debug.LogError("Thee is no parent object model");
			return string.Empty;
		}
	}

	public static string GetPartsName(in GameObject targetObject)
	{
		try
		{
			if (targetObject.CompareTag("Model"))
			{
				return "MODEL";
			}
			else if (targetObject.CompareTag("Sensor"))
			{
				return "SENSOR_" + targetObject.name;
			}
			else
			{
				return targetObject.name;
			}
		}
		catch
		{
			Debug.LogError("Thee is no parent object model");
			return string.Empty;
		}
	}

	[DllImport("StdHash")]
	public static extern ulong GetStringHashCode(string value);

	public static void Set(this messages.Time msg, in double time)
	{
		msg.Set((float)time);
	}

	public static void Set(this messages.Time msg, in float time)
	{
		if (msg == null)
		{
			msg = new messages.Time();
		}

		msg.Sec = (int)time;
		msg.Nsec = (int)((time - (double)msg.Sec) * 1e+9);
	}

	public static float Get(this messages.Time msg)
	{
		return (float)msg.Sec + ((float)msg.Nsec / (float)1e-9);
	}

	public static void SetCurrentTime(this messages.Time msg, in bool useRealTime = false)
	{
		if (msg == null)
		{
			msg = new messages.Time();
		}

		var timeNow = (useRealTime) ? GetGlobalClock().RealTime : GetGlobalClock().SimTime;
		msg.Sec = (int)timeNow;
		msg.Nsec = (int)((timeNow - (double)msg.Sec) * 1e+9);
	}

	public static void Set(this messages.Vector3d vector3d, in Vector3 position)
	{
		if (vector3d == null)
		{
			vector3d = new messages.Vector3d();
		}
		var converted = Unity2SDF.Position(position);

		vector3d.X = converted.X;
		vector3d.Y = converted.Y;
		vector3d.Z = converted.Z;
	}

	public static void SetScale(this messages.Vector3d vector3d, in Vector3 position)
	{
		if (vector3d == null)
		{
			vector3d = new messages.Vector3d();
		}
		var converted = Unity2SDF.Scale(position);

		vector3d.X = converted.X;
		vector3d.Y = converted.Y;
		vector3d.Z = converted.Z;
	}

	public static void Set(this messages.Quaternion quaternion, in Quaternion rotation)
	{
		if (quaternion == null)
		{
			quaternion = new messages.Quaternion();
		}
		var converted = Unity2SDF.Rotation(rotation);

		quaternion.X = converted.X;
		quaternion.Y = converted.Y;
		quaternion.Z = converted.Z;
		quaternion.W = converted.W;
	}

	public static bool IsSamePosition(in float A, in float B)
	{
		return (Mathf.Abs(A - B) <= Mathf.Epsilon) ? true : false;
	}

	public static bool IsSamePosition(in Vector3 A, in Vector3 B)
	{
		return (Vector3.SqrMagnitude(A - B) <= Vector3.kEpsilon) ? true : false;
	}

	public static class Convert
	{
		public static float PrismaticDirection(in float value, in Vector3 rotation)
		{
			return (Mathf.Approximately(rotation.x, 180) ||
					Mathf.Approximately(rotation.y, 180) ||
					Mathf.Approximately(rotation.z, 180)) ? -value : value;
		}
	}

	public static Vector3[] SolveConvexHull2D(in Vector3[] points)
	{
		var result = new List<Vector3>();

		if (points.Length == 0)
		{
			return result.ToArray();
		}

		int leftMostIndex = 0;
		for (var i = 1; i < points.Length; i++)
		{
			if (points[leftMostIndex].x > points[i].x)
			{
				leftMostIndex = i;
			}
		}
		result.Add(points[leftMostIndex]);

		var collinearPoints = new List<Vector3>();
		var current = points[leftMostIndex];

		while (true)
		{
			var nextTarget = points[0];
			for (var i = 1; i < points.Length; i++)
			{
				if (points[i] == current)
				{
					continue;
				}

				var x1 = current.x - nextTarget.x;
				var x2 = current.x - points[i].x;
				var z1 = current.z - nextTarget.z;
				var z2 = current.z - points[i].z;

				var val = (z2 * x1) - (z1 * x2);
				if (val > 0)
				{
					nextTarget = points[i];
					collinearPoints = new List<Vector3>();
				}
				else if (val == 0)
				{
					if (Vector3.Distance(current, nextTarget) < Vector3.Distance(current, points[i]))
					{
						collinearPoints.Add(nextTarget);
						nextTarget = points[i];
					}
					else
					{
						collinearPoints.Add(points[i]);
					}
				}
			}

			foreach (var t in collinearPoints)
			{
				result.Add(t);
			}

			if (nextTarget == points[leftMostIndex])
			{
				break;
			}

			result.Add(nextTarget);
			current = nextTarget;
		}

		var sortedList = result.OrderBy(point => Mathf.Atan2(point.z, point.x));

		return sortedList.ToArray();
	}
}