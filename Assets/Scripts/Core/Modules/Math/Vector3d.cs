/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Internal;

public struct Vector3d
{
	public double x;
	public double y;
	public double z;

	public Vector3d(in Vector3 val)
	{
		this.x = val.x;
		this.y = val.y;
		this.z = val.z;
	}

	public Vector3d(in double x, in double y)
	{
		this.x = x;
		this.y = y;
		this.z = 0;
	}

	public Vector3d(in double x, in double y, in double z)
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public double this[int index]
	{
		get
		{
			switch (index)
			{
				case 0:
					return x;
				case 1:
					return y;
				case 2:
					return z;
				default:
					throw new IndexOutOfRangeException("Invalid Vector3d index!");
			}
		}
		set
		{
			switch (index)
			{
				case 0:
					x = value;
					break;
				case 1:
					y = value;
					break;
				case 2:
					z = value;
					break;
				default:
					throw new IndexOutOfRangeException("Invalid Vector3d index!");
			}
		}
	}

	public static Vector3d back => new Vector3d(0, 0, -1);

	public static Vector3d down => new Vector3d(0, -1, 0);

	public static Vector3d forward => new Vector3d(0, 0, 1);

	public static Vector3d fwd => new Vector3d(0, 0, 1);

	public static Vector3d left => new Vector3d(-1, 0, 0);

	public static Vector3d one => new Vector3d(1, 1, 1);

	public static Vector3d right => new Vector3d(1, 0, 0);

	public static Vector3d up => new Vector3d(0, 1, 0);

	public static Vector3d zero => new Vector3d(0, 0, 0);

	public double magnitude => Math.Sqrt(sqrMagnitude);

	public Vector3d normalized => Normalize(this);

	public double sqrMagnitude => (x * x + y * y + z * z);

	public static double Angle(in Vector3d from, in Vector3d to)
	{
		var cos = Dot(from.normalized, to.normalized);
		if (cos < -1)
		{
			cos = -1;
		}
		if (cos > 1)
		{
			cos = 1;
		}
		return Math.Acos(cos) * (180d / Math.PI);
	}

	public static double AngleBetween(in Vector3d from, in Vector3d to)
	{
		var cos = Dot(from.normalized, to.normalized);
		if (cos < -1)
		{
			cos = -1;
		}
		if (cos > 1)
		{
			cos = 1;
		}
		return Math.Acos(cos);
	}

	public static Vector3d ClampMagnitude(in Vector3d vector, in double maxLength)
	{
		return (vector.sqrMagnitude > maxLength * maxLength) ? (vector.normalized * maxLength) : vector;
	}

	public static Vector3d Cross(in Vector3d lhs, in Vector3d rhs)
	{
		var x = lhs.y * rhs.z - rhs.y * lhs.z;
		var y = lhs.z * rhs.x - rhs.z * lhs.x;
		var z = lhs.x * rhs.y - rhs.x * lhs.y;
		return new Vector3d(x, y, z);
	}

	public static double Distance(in Vector3d a, in Vector3d b)
	{
		return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z));
	}

	public static double Dot(in Vector3d lhs, in Vector3d rhs)
	{
		return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;
	}

	public static Vector3d Exclude(in Vector3d excludeThis, in Vector3d fromThat)
	{
		return fromThat - Project(fromThat, excludeThis);
	}

	public static Vector3d Lerp(in Vector3d a, in Vector3d b, in double t)
	{
		if (t <= 0)
		{
			return a;
		}
		else if (t >= 1)
		{
			return b;
		}
		return a + (b - a) * t;
	}

	public static Vector3d LerpUnclamped(in Vector3d a, in Vector3d b, in double t)
	{
		return a + (b - a) * t;
	}

	public static double Magnitude(in Vector3d a)
	{
		return a.magnitude;
	}

	public static Vector3d Max(in Vector3d lhs, in Vector3d rhs)
	{
		var temp = new Vector3d();
		temp.x = Math.Max(lhs.x, rhs.x);
		temp.y = Math.Max(lhs.y, rhs.y);
		temp.z = Math.Max(lhs.z, rhs.z);
		return temp;
	}

	public static Vector3d Min(in Vector3d lhs, in Vector3d rhs)
	{
		var temp = new Vector3d();
		temp.x = Math.Min(lhs.x, rhs.x);
		temp.y = Math.Min(lhs.y, rhs.y);
		temp.z = Math.Min(lhs.z, rhs.z);
		return temp;
	}

	public static Vector3d MoveTowards(in Vector3d current, in Vector3d target, in double maxDistanceDelta)
	{
		var vector3 = target - current;
		var single = vector3.magnitude;
		if (single <= maxDistanceDelta || single == 0f)
		{
			return target;
		}
		return current + ((vector3 / single) * maxDistanceDelta);
	}

	public static Vector3d Normalize(in Vector3d value)
	{
		if (value == zero)
		{
			return zero;
		}
		else
		{
			var tempDVec = new Vector3d();
			tempDVec.x = value.x / value.magnitude;
			tempDVec.y = value.y / value.magnitude;
			tempDVec.z = value.z / value.magnitude;
			return tempDVec;
		}
	}

	public static void OrthoNormalize(ref Vector3d normal, ref Vector3d tangent)
	{
		var mag = Magnitude(normal);
		if (mag > 0)
			normal /= mag;
		else
			normal = new Vector3d(1, 0, 0);

		var dot0 = Dot(normal, tangent);
		tangent -= dot0 * normal;
		mag = Magnitude(tangent);
		if (mag < 0)
			tangent = OrthoNormalVectorFast(normal);
		else
			tangent /= mag;
	}

	public static void OrthoNormalize(ref Vector3d normal, ref Vector3d tangent, ref Vector3d binormal)
	{
		var mag = Magnitude(normal);
		if (mag > 0)
			normal /= mag;
		else
			normal = new Vector3d(1, 0, 0);

		var dot0 = Dot(normal, tangent);
		tangent -= dot0 * normal;
		mag = Magnitude(tangent);
		if (mag > 0)
			tangent /= mag;
		else
			tangent = OrthoNormalVectorFast(normal);

		var dot1 = Dot(tangent, binormal);
		dot0 = Dot(normal, binormal);
		binormal -= dot0 * normal + dot1 * tangent;
		mag = Magnitude(binormal);
		if (mag > 0)
			binormal /= mag;
		else
			binormal = Cross(normal, tangent);
	}

	public static Vector3d Project(in Vector3d vector, in Vector3d onNormal)
	{
		if (vector == zero || onNormal == zero)
		{
			return zero;
		}
		return Dot(vector, onNormal) / (onNormal.magnitude * onNormal.magnitude) * onNormal;
	}

	public static Vector3d ProjectOnPlane(in Vector3d vector, in Vector3d planeNormal)
	{
		return vector - Project(vector, planeNormal);
	}

	public static Vector3d Reflect(in Vector3d inDirection, in Vector3d inNormal)
	{
		return (-2f * Dot(inNormal, inDirection)) * inNormal + inDirection;
	}

	public static Vector3d RotateTowards(in Vector3d current, in Vector3d target, in double maxRadiansDelta, in double maxMagnitudeDelta)
	{
		var currentMag = Magnitude(current);
		var targetMag = Magnitude(target);

		if (currentMag > 0 && targetMag > 0)
		{
			Vector3d currentNorm = current / currentMag;
			Vector3d targetNorm = target / targetMag;

			double dot = Dot(currentNorm, targetNorm);

			if (dot > 1)
			{
				return MoveTowards(current, target, maxMagnitudeDelta);
			}
			else if (dot < -1)
			{
				Vector3d axis = OrthoNormalVectorFast(currentNorm);
				Matrix4x4d m = SetAxisAngle(axis, maxRadiansDelta);
				Vector3d rotated = m * currentNorm;
				rotated *= ClampedMove(currentMag, targetMag, maxMagnitudeDelta);
				return rotated;
			}
			else
			{
				double angle = Math.Acos(dot);
				Vector3d axis = Normalize(Cross(currentNorm, targetNorm));
				Matrix4x4d m = SetAxisAngle(axis, Math.Min(maxRadiansDelta, angle));
				Vector3d rotated = m * currentNorm;
				rotated *= ClampedMove(currentMag, targetMag, maxMagnitudeDelta);
				return rotated;
			}
		}
		else
		{
			return MoveTowards(current, target, maxMagnitudeDelta);
		}
	}

	public static Vector3d Scale(in Vector3d a, in Vector3d b)
	{
		var temp = new Vector3d();
		temp.x = a.x * b.x;
		temp.y = a.y * b.y;
		temp.z = a.z * b.z;
		return temp;
	}

	public static Vector3d Slerp(in Vector3d lhs, in Vector3d rhs, double t)
	{
		if (t < 0)
		{
			t = 0;
		}

		if (t > 1)
		{
			t = 1;
		}

		return SlerpUnclamped(lhs, rhs, t);
	}

	public static Vector3d SlerpUnclamped(in Vector3d lhs, in Vector3d rhs, in double t)
	{
		var lhsMag = Magnitude(lhs);
		var rhsMag = Magnitude(rhs);

		if (lhsMag < 0 || rhsMag < 0)
		{
			return Lerp(lhs, rhs, t);
		}

		var lerpedMagnitude = rhsMag * t + lhsMag * (1 - t);

		var dot = Dot(lhs, rhs) / (lhsMag * rhsMag);

		if (dot > 1)
		{
			return Lerp(lhs, rhs, t);
		}
		else if (dot < -1)
		{
			var lhsNorm = lhs / lhsMag;
			var axis = OrthoNormalVectorFast(lhsNorm);
			var m = SetAxisAngle(axis, Math.PI * t);
			var slerped = m * lhsNorm;
			slerped *= lerpedMagnitude;
			return slerped;
		}
		else
		{
			var axis = Cross(lhs, rhs);
			var lhsNorm = lhs / lhsMag;
			axis = Normalize(axis);
			var angle = Math.Acos(dot) * t;

			var m = SetAxisAngle(axis, angle);
			var slerped = m * lhsNorm;
			slerped *= lerpedMagnitude;
			return slerped;
		}
	}

	public static Vector3d SmoothDamp(in Vector3d current, in Vector3d target, ref Vector3d currentVelocity, in double smoothTime, in double maxSpeed)
	{
		var deltaTime = Time.deltaTime;
		return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
	}

	public static Vector3d SmoothDamp(in Vector3d current, in Vector3d target, ref Vector3d currentVelocity, in double smoothTime)
	{
		var deltaTime = Time.deltaTime;
		var maxSpeed = double.PositiveInfinity;
		return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
	}

	public static Vector3d SmoothDamp(in Vector3d current, Vector3d target, ref Vector3d currentVelocity, double smoothTime, [DefaultValue("Mathf.Infinity")] double maxSpeed, [DefaultValue("Time.deltaTime")] double deltaTime)
	{
		smoothTime = Math.Max(0.0001, smoothTime);
		var num = 2f / smoothTime;
		var num2 = num * deltaTime;
		var d = 1f / (1f + num2 + 0.48 * num2 * num2 + 0.235 * num2 * num2 * num2);
		var vector = current - target;
		var vector2 = target;
		double maxLength = maxSpeed * smoothTime;
		vector = ClampMagnitude(vector, maxLength);
		target = current - vector;

		var vector3 = (currentVelocity + num * vector) * deltaTime;
		currentVelocity = (currentVelocity - num * vector3) * d;

		var vector4 = target + (vector + vector3) * d;
		if (Dot(vector2 - current, vector4 - vector2) > 0f)
		{
			vector4 = vector2;
			currentVelocity = (vector4 - vector2) / deltaTime;
		}

		return vector4;
	}

	public static double SqrMagnitude(in Vector3d a)
	{
		return a.sqrMagnitude;
	}

	public void Normalize()
	{
		if (this != zero)
		{
			var length = magnitude;
			x /= length;
			y /= length;
			z /= length;
		}
	}

	public void Scale(in Vector3d scale)
	{
		x *= scale.x;
		y *= scale.y;
		z *= scale.z;
	}

	public void Set(in double new_x, in double new_y, in double new_z)
	{
		x = new_x;
		y = new_y;
		z = new_z;
	}

	public override string ToString()
	{
		return String.Format("({0}, {1}, {2})", x, y, z);
	}

	public override int GetHashCode()
	{
		return this.x.GetHashCode() ^ this.y.GetHashCode() << 2 ^ this.z.GetHashCode() >> 2;
	}

	public override bool Equals(object other)
	{
		return this == (Vector3d)other;
	}

	public string ToString(string format)
	{
		return String.Format("({0}, {1}, {2})", x.ToString(format), y.ToString(format), z.ToString(format));
	}

	public Vector3 ToVector3()
	{
		return new Vector3((float)x, (float)y, (float)z);
	}

	private static Vector3d OrthoNormalVectorFast(in Vector3d normal)
	{
		double k1OverSqrt2 = Math.Sqrt(0.5);
		Vector3d res;
		if (Math.Abs(normal.z) > k1OverSqrt2)
		{
			double a = normal.y * normal.y + normal.z * normal.z;
			double k = 1 / Math.Sqrt(a);
			res.x = 0;
			res.y = -normal.z * k;
			res.z = normal.y * k;
		}
		else
		{
			double a = normal.x * normal.x + normal.y * normal.y;
			double k = 1 / Math.Sqrt(a);
			res.x = -normal.y * k;
			res.y = normal.x * k;
			res.z = 0;
		}
		return res;
	}

	private static double ClampedMove(in double lhs, in double rhs, in double clampedDelta)
	{
		var delta = rhs - lhs;
		return lhs + ((delta > 0.0F) ? Math.Min(delta, clampedDelta) : -Math.Min(-delta, clampedDelta));
	}

	public static Matrix4x4d SetAxisAngle(in Vector3d rotationAxis, in double radians)
	{
		var m = new Matrix4x4d();

		var s = Math.Sin(radians);
		var c = Math.Cos(radians);

		var xx = rotationAxis.x * rotationAxis.x;
		var yy = rotationAxis.y * rotationAxis.y;
		var zz = rotationAxis.z * rotationAxis.z;
		var xy = rotationAxis.x * rotationAxis.y;
		var yz = rotationAxis.y * rotationAxis.z;
		var zx = rotationAxis.z * rotationAxis.x;
		var xs = rotationAxis.x * s;
		var ys = rotationAxis.y * s;
		var zs = rotationAxis.z * s;
		var one_c = 1 - c;

		m[0, 0] = (one_c * xx) + c;
		m[0, 1] = (one_c * xy) - zs;
		m[0, 2] = (one_c * zx) + ys;

		m[1, 0] = (one_c * xy) + zs;
		m[1, 1] = (one_c * yy) + c;
		m[1, 2] = (one_c * yz) - xs;

		m[2, 0] = (one_c * zx) - ys;
		m[2, 1] = (one_c * yz) + xs;
		m[2, 2] = (one_c * zz) + c;

		return m;
	}

	public static Vector3 operator +(in Vector3 a, in Vector3d b)
	{
		return new Vector3(a.x + (float)b.x, a.y + (float)b.y, a.z + (float)b.z);
	}

	public static Vector3d operator +(in Vector3d a, in Vector3d b)
	{
		return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	public static Vector3d operator -(in Vector3d a)
	{
		return new Vector3d(-a.x, -a.y, -a.z);
	}

	public static Vector3d operator -(in Vector3d a, in Vector3d b)
	{
		return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
	}

	public static Vector3d operator *(in double d, in Vector3d a)
	{
		return new Vector3d(a.x * d, a.y * d, a.z * d);
	}

	public static Vector3d operator *(in Vector3d a, in double d)
	{
		return new Vector3d(a.x * d, a.y * d, a.z * d);
	}

	public static Vector3d operator /(in Vector3d a, in double d)
	{
		return new Vector3d(a.x / d, a.y / d, a.z / d);
	}

	public static bool operator ==(in Vector3d lhs, in Vector3d rhs)
	{
		return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z) ? true : false;
	}

	public static bool operator !=(in Vector3d lhs, in Vector3d rhs)
	{
		return !(lhs == rhs);
	}

	public static implicit operator Vector3d(in Vector3 v)
	{
		return new Vector3d(v.x, v.y, v.z);
	}

	public static implicit operator Vector3(in Vector3d v)
	{
		return new Vector3((float)v.x, (float)v.y, (float)v.z);
	}
}