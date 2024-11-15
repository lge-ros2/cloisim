/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public struct Vector4d
{
	public readonly int Size;
	public double x;
	public double y;
	public double z;
	public double w;

	public Vector4d(in double x)
		: this(x, 0, 0, 0)
	{
	}

	public Vector4d(in double x, in double y)
		: this(x, y, 0, 0)
	{
	}

	public Vector4d(in double x, in double y, in double z)
		: this(x, y, z, 0)
	{
	}

	public Vector4d(in Vector4d v)
		: this(v.x, v.y, v.z, v.w)
	{
	}

	public Vector4d(in double x, in double y, in double z, in double w)
	{
		this.Size = 4;
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
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
				case 3:
					return w;
				default:
					throw new IndexOutOfRangeException("Invalid Vector4d index!");
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
				case 3:
					w = value;
					break;
				default:
					throw new IndexOutOfRangeException("Invalid Vector4d index!");
			}
		}
	}

	public static Vector4d one => new Vector4d(1, 1, 1, 1);

	public static Vector4d zero => new Vector4d(0, 0, 0, 0);

	public double magnitude => Math.Sqrt(sqrMagnitude);

	public Vector4d normalized => Vector4.Normalize(this);

	public double sqrMagnitude => (x * x + y * y + z * z + w * w);


	public static double Distance(in Vector4d a, in Vector4d b)
	{
		return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z) + (a.w - b.w) * (a.w - b.w));
	}

	public static double Dot(in Vector4d lhs, in Vector4d rhs)
	{
		return lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z + lhs.w * rhs.w;
	}

	public static Vector4d Lerp(in Vector4d a, in Vector4d b, float t)
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

	public static Vector4d LerpUnclamped(in Vector4d a, in Vector4d b, in double t)
	{
		return a + (b - a) * t;
	}

	public static double Magnitude(in Vector4d a)
	{
		return a.magnitude;
	}

	public static Vector4d Max(in Vector4d lhs, in Vector4d rhs)
	{
		return new Vector4d(
			x: Math.Max(lhs.x, rhs.x),
			y: Math.Max(lhs.y, rhs.y),
			z: Math.Max(lhs.z, rhs.z),
			w: Math.Max(lhs.w, rhs.w)
		);
	}

	public static Vector4d Min(in Vector4d lhs, in Vector4d rhs)
	{
		return new Vector4d(
			x: Math.Min(lhs.x, rhs.x),
			y: Math.Min(lhs.y, rhs.y),
			z: Math.Min(lhs.z, rhs.z),
			w: Math.Min(lhs.w, rhs.w)
		);
	}

	public static Vector4d MoveTowards(in Vector4d current, in Vector4d target, in double maxDistanceDelta)
	{
		var vector4 = target - current;
		var single = vector4.magnitude;
		if (single <= maxDistanceDelta || single == 0f)
		{
			return target;
		}
		return current + ((vector4 / single) * maxDistanceDelta);
	}

	public static Vector4d Normalize(in Vector4d value)
	{
		if (value == zero)
		{
			return zero;
		}
		else
		{
			var tempDVec = new Vector4d(value);
			tempDVec.x /= value.magnitude;
			tempDVec.y /= value.magnitude;
			tempDVec.z /= value.magnitude;
			tempDVec.w /= value.magnitude;
			return tempDVec;
		}
	}

	public static Vector4d Project(in Vector4d vector, in Vector4d onNormal)
	{
		if (vector == zero || onNormal == zero)
		{
			return zero;
		}
		return Dot(vector, onNormal) / (onNormal.magnitude * onNormal.magnitude) * onNormal;
	}

	public static Vector4d Scale(in Vector4d a, in Vector4d b)
	{
		return new Vector4d(
			x: a.x * b.x,
			y: a.y * b.y,
			z: a.z * b.z,
			w: a.w * b.w
		);
	}

	public static double SqrMagnitude(in Vector4d a)
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
			w /= length;
		}
	}

	public void Scale(in double scale)
	{
		x *= scale;
		y *= scale;
		z *= scale;
		w *= scale;
	}

	public void Scale(in Vector4d scale)
	{
		x *= scale.x;
		y *= scale.y;
		z *= scale.z;
		w *= scale.w;
	}

	public void Set(in double new_x, in double new_y, in double new_z, in double new_w)
	{
		this.x = new_x;
		this.y = new_y;
		this.z = new_z;
		this.w = new_w;
	}

	public double SqrMagnitude()
	{
		return sqrMagnitude;
	}

	public override string ToString()
	{
		return String.Format("({0}, {1}, {2}, {3})", x, y, z, w);
	}

	public string ToString(string format)
	{
		return String.Format("({0}, {1}, {2}, {3})", x.ToString(format), y.ToString(format), z.ToString(format), w.ToString(format));
	}

	public override int GetHashCode()
	{
		return this.x.GetHashCode() ^ this.y.GetHashCode() << 2 ^ this.z.GetHashCode() >> 2 ^ this.w.GetHashCode() >> 1;
	}

	public override bool Equals(object other)
	{
		return this == (Vector4d)other;
	}

	public Vector4 ToVector4()
	{
		return new Vector4((float)x, (float)y, (float)z, (float)w);
	}

	public static Vector4d operator +(in Vector4d a, in Vector4d b)
	{
		return new Vector4d(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
	}

	public static Vector4d operator -(in Vector4d a)
	{
		return new Vector4d(-a.x, -a.y, -a.z, -a.w);
	}

	public static Vector4d operator -(in Vector4d a, in Vector4d b)
	{
		return new Vector4d(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
	}

	public static Vector4d operator *(in double d, in Vector4d a)
	{
		return new Vector4d(a.x * d, a.y * d, a.z * d, a.w * d);
	}

	public static Vector4d operator *(in Vector4d a, in double d)
	{
		return new Vector4d(a.x * d, a.y * d, a.z * d, a.w * d);
	}

	public static Vector4d operator /(in Vector4d a, in double d)
	{
		return new Vector4d(a.x / d, a.y / d, a.z / d, a.w / d);
	}

	public static bool operator ==(in Vector4d lhs, in Vector4d rhs)
	{
		return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.w == rhs.w) ? true : false;
	}

	public static bool operator !=(in Vector4d lhs, in Vector4d rhs)
	{
		return !(lhs == rhs);
	}

	public static implicit operator Vector4d(in Vector2d v)
	{
		return new Vector4d(v.x, v.y, 0, 0);
	}

	public static implicit operator Vector4d(in Vector3d v)
	{
		return new Vector4d(v.x, v.y, v.z, 0);
	}

	public static implicit operator Vector2d(in Vector4d v)
	{
		return new Vector2d(v.x, v.y);
	}

	public static implicit operator Vector3d(in Vector4d v)
	{
		return new Vector3d(v.x, v.y, v.z);
	}

	public static implicit operator Vector4d(in Vector4 v)
	{
		return new Vector4d(v.x, v.y, v.z, v.w);
	}

	public static implicit operator Vector4(in Vector4d v)
	{
		return new Vector4((float)v.x, (float)v.y, (float)v.z, (float)v.w);
	}
}