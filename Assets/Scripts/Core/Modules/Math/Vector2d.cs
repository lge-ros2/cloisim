/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public struct Vector2d
{
	public double x;
	public double y;

	public Vector2d(in double p_x, in double p_y)
	{
		x = p_x;
		y = p_y;
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
				default:
					throw new IndexOutOfRangeException("Invalid Vector2d index!");
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
				default:
					throw new IndexOutOfRangeException("Invalid Vector2d index!");
			}
		}
	}

	public static Vector2d down => new Vector2d(0, -1);

	public static Vector2d left => new Vector2d(-1, 0);

	public static Vector2d one => new Vector2d(1, 1);

	public static Vector2d right => new Vector2d(1, 0);

	public static Vector2d up => new Vector2d(0, 1);

	public static Vector2d zero => new Vector2d(0, 0);

	public double magnitude => Math.Sqrt(sqrMagnitude);

	public Vector2d normalized
	{
		get
		{
			var result = new Vector2d(x, y);
			result.Normalize();
			return result;
		}
	}
	public double sqrMagnitude
	{
		get
		{
			return x * x + y * y;
		}
	}

	public static double Angle(in Vector2d from, in Vector2d to)
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

	public static Vector2d ClampMagnitude(in Vector2d vector, in double maxLength)
	{
		return (maxLength * maxLength >= vector.sqrMagnitude) ? vector : vector.normalized * maxLength;
	}

	public static double Distance(in Vector2d a, in Vector2d b)
	{
		return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
	}

	public static double Dot(in Vector2d lhs, in Vector2d rhs)
	{
		return lhs.x * rhs.x + lhs.y * rhs.y;
	}

	public static Vector2d Lerp(in Vector2d a, in Vector2d b, in double t)
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

	public static Vector2d LerpUnclamped(in Vector2d a, in Vector2d b, in double t)
	{
		return a + (b - a) * t;
	}

	public static Vector2d Max(in Vector2d lhs, in Vector2d rhs)
	{
		var temp = new Vector2d();
		temp.x = Math.Max(lhs.x, rhs.x);
		temp.y = Math.Max(lhs.y, rhs.y);
		return temp;
	}

	public static Vector2d Min(in Vector2d lhs, in Vector2d rhs)
	{
		var temp = new Vector2d();
		temp.x = Math.Min(lhs.x, rhs.x);
		temp.y = Math.Min(lhs.y, rhs.y);
		return temp;
	}

	public static Vector2d MoveTowards(in Vector2d current, in Vector2d target, in double maxDistanceDelta)
	{
		var vector2 = target - current;
		var single = vector2.magnitude;
		if (single <= maxDistanceDelta || single == 0f)
		{
			return target;
		}
		return current + ((vector2 / single) * maxDistanceDelta);
	}

	public static Vector2d Reflect(in Vector2d inDirection, in Vector2d inNormal)
	{
		return (-2f * Dot(inNormal, inDirection)) * inNormal + inDirection;
	}

	public static Vector2d Scale(in Vector2d a, in Vector2d b)
	{
		var temp = new Vector2d();
		temp.x = a.x * b.x;
		temp.y = a.y * b.y;
		return temp;
	}

	public static Vector2d SmoothDamp(in Vector2d current, Vector2d target, ref Vector2d currentVelocity, double smoothTime, in double maxSpeed, in double deltaTime)
	{
		smoothTime = Math.Max(0.0001, smoothTime);
		var num = 2 / smoothTime;
		var num2 = num * deltaTime;
		var d = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
		var vector = current - target;
		var vector2 = target;
		var maxLength = maxSpeed * smoothTime;

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

	public static double SqrMagnitude(in Vector2d a)
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
		}
	}

	public void Scale(in Vector2d scale)
	{
		x *= scale.x;
		y *= scale.y;
	}

	public void Set(in double newX, in double newY)
	{
		x = newX;
		y = newY;
	}

	public double SqrMagnitude()
	{
		return sqrMagnitude;
	}

	public override string ToString()
	{
		return String.Format("({0}, {1})", x, y);
	}

	public override bool Equals(object other)
	{
		return this == (Vector2d)other;
	}

	public string ToString(string format)
	{
		return String.Format("({0}, {1})", x.ToString(format), y.ToString(format));
	}

	public override int GetHashCode()
	{
		return this.x.GetHashCode() ^ this.y.GetHashCode() << 2;
	}

	public Vector2 ToVector2()
	{
		return new Vector2((float)x, (float)y);
	}

	public static Vector2d operator +(in Vector2d a, in Vector2d b)
	{
		return new Vector2d(a.x + b.x, a.y + b.y);
	}

	public static Vector2d operator -(in Vector2d a)
	{
		return new Vector2d(-a.x, -a.y);
	}

	public static Vector2d operator -(in Vector2d a, in Vector2d b)
	{
		return new Vector2d(a.x - b.x, a.y - b.y);
	}

	public static Vector2d operator *(in double d, in Vector2d a)
	{
		return new Vector2d(a.x * d, a.y * d);
	}

	public static Vector2d operator *(in Vector2d a, in double d)
	{
		return new Vector2d(a.x * d, a.y * d);
	}

	public static Vector2d operator /(in Vector2d a, in double d)
	{
		return new Vector2d(a.x / d, a.y / d);
	}

	public static bool operator ==(in Vector2d lhs, in Vector2d rhs)
	{
		return (lhs.x == rhs.x && lhs.y == rhs.y) ? true : false;
	}

	public static bool operator !=(in Vector2d lhs, in Vector2d rhs)
	{
		return !(lhs == rhs);
	}

	public static implicit operator Vector2d(in Vector3d v)
	{
		return new Vector2d(v.x, v.y);
	}

	public static implicit operator Vector3d(in Vector2d v)
	{
		return new Vector3d(v.x, v.y, 0);
	}

	public static implicit operator Vector2d(in Vector2 v)
	{
		return new Vector2d(v.x, v.y);
	}

	public static implicit operator Vector2(in Vector2d v)
	{
		return new Vector2((float)v.x, (float)v.y);
	}
}