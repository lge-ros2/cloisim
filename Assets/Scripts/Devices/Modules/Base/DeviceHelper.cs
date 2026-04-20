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
	private static double Sec2NSec = 1e+9;
	private static double NSec2Sec = 1 / Sec2NSec;

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
			SDFormat.Helper.Base nextObject = targetObject.GetComponentInParent<SDFormat.Helper.Model>();

			if (nextObject == null)
			{
				nextObject = targetObject.GetComponentInParent<SDFormat.Helper.Actor>();
			}

			if (nextObject != null && !nextObject.CompareTag("Actor"))
			{
				while (!nextObject.transform.IsRootModel())
				{
					nextObject = nextObject.transform.parent.GetComponentInParent<SDFormat.Helper.Base>();

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
				// Debug.Log($"Parts Name: {targetObject.name}");
				return targetObject.name;
			}
			else if (targetObject.CompareTag("Sensor"))
			{
				var linkHelper = targetObject.GetComponentInParent<SDFormat.Helper.Link>();
				if (linkHelper == null)
				{
					// Debug.LogWarning($"There is no Link helper for sensor: {targetObject.name}");
					return targetObject.name;
				}
				else
				{
					if (linkHelper.Model.Equals(linkHelper.RootModel))
					{
						return $"{linkHelper.name}_{targetObject.name}";
					}
					else
					{
						return $"{linkHelper.Model.name}_{targetObject.name}";
					}
				}
			}
			else
			{
				var linkHelper = targetObject.GetComponentInParent<SDFormat.Helper.Link>();
				if (linkHelper.transform.parent.CompareTag("Link")) // if sensor link is nested in link element
				{
					// Debug.Log($"Parts Name: {linkHelper.Model.name}");
					return linkHelper.name; // link name
				}
				else
				{
					// Debug.Log($"Parts Name: {linkHelper.Model.name}");
					return linkHelper.Model.name; // model name
				}
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
		if (msg == null)
		{
			msg = new messages.Time();
		}

		msg.Sec = (int)time;
		msg.Nsec = (int)((time - (double)msg.Sec) * Sec2NSec);
	}

	public static void Set(this messages.Time msg, in float time)
	{
		msg.Set((double)time);
	}

	public static float Get(this messages.Time msg)
	{
		return (float)((double)msg.Sec + ((double)msg.Nsec * NSec2Sec));
	}

	public static void SetCurrentTime(this messages.Time msg, in bool useRealTime = false)
	{
		if (msg == null)
		{
			msg = new messages.Time();
		}

		var timeNow = (useRealTime) ? GetGlobalClock().RealTime : GetGlobalClock().SimTime;
		msg.Sec = (int)timeNow;
		msg.Nsec = (int)((timeNow - (double)msg.Sec) * Sec2NSec);
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

	public static Vector3[] SolveConvexHull2D(this Vector3[] points)
	{
		var n = points.Length;
		if (n == 0)
		{
			return System.Array.Empty<Vector3>();
		}

		// Andrew's monotone chain algorithm — O(n log n)
		var sorted = new Vector3[n];
		System.Array.Copy(points, sorted, n);
		System.Array.Sort(sorted, (a, b) =>
		{
			var cmp = a.x.CompareTo(b.x);
			return cmp != 0 ? cmp : a.z.CompareTo(b.z);
		});

		var hull = new Vector3[2 * n];
		var k = 0;

		// Build lower hull
		for (var i = 0; i < n; i++)
		{
			while (k >= 2 && Cross2D(hull[k - 2], hull[k - 1], sorted[i]) <= 0)
			{
				k--;
			}
			hull[k++] = sorted[i];
		}

		// Build upper hull
		var lower = k + 1;
		for (var i = n - 2; i >= 0; i--)
		{
			while (k >= lower && Cross2D(hull[k - 2], hull[k - 1], sorted[i]) <= 0)
			{
				k--;
			}
			hull[k++] = sorted[i];
		}

		// Last point equals first point, so omit it
		var result = new Vector3[k - 1];
		System.Array.Copy(hull, result, k - 1);
		return result;
	}

	private static float Cross2D(in Vector3 o, in Vector3 a, in Vector3 b)
	{
		return (a.x - o.x) * (b.z - o.z) - (a.z - o.z) * (b.x - o.x);
	}
}