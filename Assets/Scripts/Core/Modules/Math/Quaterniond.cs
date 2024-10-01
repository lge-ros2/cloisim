/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine.Internal;

public struct Quaterniond
{
	public double x;
	public double y;
	public double z;
	public double w;

	public Quaterniond(in double x, in double y, in double z, in double w)
	{
		this.x = x;
		this.y = y;
		this.z = z;
		this.w = w;
	}

	public double this[in int index]
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
					throw new IndexOutOfRangeException("Invalid Quaterniond index!");
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
					throw new IndexOutOfRangeException("Invalid Quaterniond index!");
			}
		}
	}

	public static Quaterniond identity => new Quaterniond(0, 0, 0, 1);

	public Vector3d eulerAngles
	{
		get
		{
			var m = QuaternionToMatrix(this);
			return (MatrixToEuler(m) * 180 / Math.PI);
		}
		set
		{
			this = Euler(value);
		}
	}

	public static double Angle(in Quaterniond a, in Quaterniond b)
	{
		var single = Dot(a, b);
		return Math.Acos(Math.Min(Math.Abs(single), 1f)) * 2f * (180 / Math.PI);
	}

	public static Quaterniond AngleAxis(double angle, Vector3d axis)
	{
		axis = axis.normalized;
		angle = angle / 180D * Math.PI;

		var halfAngle = angle * 0.5D;
		var s = Math.Sin(halfAngle);

		return new Quaterniond(
			x: s * axis.x,
			y: s * axis.y,
			z: s * axis.z,
			w: Math.Cos(halfAngle)
		);
	}

	public static double Dot(in Quaterniond a, in Quaterniond b)
	{
		return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
	}

	public static Quaterniond Euler(in Vector3d euler)
	{
		return Euler(euler.x, euler.y, euler.z);
	}

	public static Quaterniond Euler(in double x, in double y, in double z)
	{
		var cX = Math.Cos(x * Math.PI / 360);
		var sX = Math.Sin(x * Math.PI / 360);

		var cY = Math.Cos(y * Math.PI / 360);
		var sY = Math.Sin(y * Math.PI / 360);

		var cZ = Math.Cos(z * Math.PI / 360);
		var sZ = Math.Sin(z * Math.PI / 360);

		var qX = new Quaterniond(sX, 0, 0, cX);
		var qY = new Quaterniond(0, sY, 0, cY);
		var qZ = new Quaterniond(0, 0, sZ, cZ);

		return (qY * qX) * qZ;
	}

	public static Quaterniond FromToRotation(in Vector3d fromDirection, in Vector3d toDirection)
	{
		throw new IndexOutOfRangeException("Not Available!");
	}

	public static Quaterniond Inverse(in Quaterniond rotation)
	{
		return new Quaterniond(-rotation.x, -rotation.y, -rotation.z, rotation.w);
	}

	public static Quaterniond Lerp(in Quaterniond a, in Quaterniond b, double t)
	{
		if (t > 1)
		{
			t = 1;
		}

		if (t < 0)
		{
			t = 0;
		}

		return LerpUnclamped(a, b, t);
	}

	public static Quaterniond LerpUnclamped(in Quaterniond a, in Quaterniond b, in double t)
	{
		var tmpQuat = new Quaterniond();

		if (Dot(a, b) < 0.0F)
		{
			tmpQuat.Set(a.x + t * (-b.x - a.x),
						a.y + t * (-b.y - a.y),
						a.z + t * (-b.z - a.z),
						a.w + t * (-b.w - a.w));
		}
		else
		{
			tmpQuat.Set(a.x + t * (b.x - a.x),
						a.y + t * (b.y - a.y),
						a.z + t * (b.z - a.z),
						a.w + t * (b.w - a.w));
		}

		var nor = Math.Sqrt(Dot(tmpQuat, tmpQuat));
		return new Quaterniond(tmpQuat.x / nor, tmpQuat.y / nor, tmpQuat.z / nor, tmpQuat.w / nor);
	}

	public static Quaterniond LookRotation(in Vector3d forward)
	{
		var up = Vector3d.up;
		return LookRotation(forward, up);
	}

	public static Quaterniond LookRotation(in Vector3d forward, [DefaultValue("Vector3d.up")] in Vector3d upwards)
	{
		var m = LookRotationToMatrix(forward, upwards);
		return MatrixToQuaternion(m);
	}

	public static Quaterniond RotateTowards(in Quaterniond from, in Quaterniond to, in double maxDegreesDelta)
	{
		var num = Quaterniond.Angle(from, to);
		var result = new Quaterniond();
		if (num == 0f)
		{
			result = to;
		}
		else
		{
			var t = Math.Min(1f, maxDegreesDelta / num);
			result = Quaterniond.SlerpUnclamped(from, to, t);
		}
		return result;
	}

	public static Quaterniond Slerp(in Quaterniond a, in Quaterniond b, double t)
	{
		if (t > 1)
		{
			t = 1;
		}

		if (t < 0)
		{
			t = 0;
		}

		return SlerpUnclamped(a, b, t);
	}

	public static Quaterniond SlerpUnclamped(in Quaterniond q1, in Quaterniond q2, in double t)
	{
		var dot = Dot(q1, q2);

		var tmpQuat = new Quaterniond();
		if (dot < 0)
		{
			dot = -dot;
			tmpQuat.Set(-q2.x, -q2.y, -q2.z, -q2.w);
		}
		else
		{
			tmpQuat = q2;
		}

		if (dot < 1)
		{
			var angle = Math.Acos(dot);
			var sinadiv = 1 / Math.Sin(angle);
			var sinat = Math.Sin(angle * t);
			var sinaomt = Math.Sin(angle * (1 - t));
			tmpQuat.Set((q1.x * sinaomt + tmpQuat.x * sinat) * sinadiv,
					 (q1.y * sinaomt + tmpQuat.y * sinat) * sinadiv,
					 (q1.z * sinaomt + tmpQuat.z * sinat) * sinadiv,
					 (q1.w * sinaomt + tmpQuat.w * sinat) * sinadiv);
			return tmpQuat;

		}
		else
		{
			return Lerp(q1, tmpQuat, t);
		}
	}

	public void Set(in double new_x, in double new_y, in double new_z, in double new_w)
	{
		this.x = new_x;
		this.y = new_y;
		this.z = new_z;
		this.w = new_w;
	}

	public void SetFromToRotation(in Vector3d fromDirection, in Vector3d toDirection)
	{
		this = FromToRotation(fromDirection, toDirection);
	}

	public void SetLookRotation(in Vector3d view)
	{
		this = LookRotation(view);
	}

	public void SetLookRotation(in Vector3d view, [DefaultValue("Vector3d.up")] in Vector3d up)
	{
		this = LookRotation(view, up);
	}

	public void ToAngleAxis(out double angle, out Vector3d axis)
	{
		angle = 2.0f * Math.Acos(w);
		if (angle == 0)
		{
			axis = Vector3d.right;
			return;
		}

		var div = 1.0f / Math.Sqrt(1 - w * w);
		axis = new Vector3d();
		axis.Set(x * div, y * div, z * div);
		angle = angle * 180D / Math.PI;
	}

	public override string ToString()
	{
		return String.Format("({0}, {1}, {2}, {3})", x, y, z, w);
	}

	public override int GetHashCode()
	{
		return this.x.GetHashCode() ^ this.y.GetHashCode() << 2 ^ this.z.GetHashCode() >> 2 ^ this.w.GetHashCode() >> 1;
	}

	public override bool Equals(object other)
	{
		return this == (Quaterniond)other;
	}

	public string ToString(in string format)
	{
		return String.Format("({0}, {1}, {2}, {3})", x.ToString(format), y.ToString(format), z.ToString(format), w.ToString(format));
	}

	private Vector3d MatrixToEuler(in Matrix4x4d m)
	{
		var v = Vector3d.zero;
		if (m[1, 2] < 1)
		{
			if (m[1, 2] > -1)
			{
				v.x = Math.Asin(-m[1, 2]);
				v.y = Math.Atan2(m[0, 2], m[2, 2]);
				v.z = Math.Atan2(m[1, 0], m[1, 1]);
			}
			else
			{
				v.x = Math.PI * 0.5;
				v.y = Math.Atan2(m[0, 1], m[0, 0]);
				v.z = 0;
			}
		}
		else
		{
			v.x = -Math.PI * 0.5;
			v.y = Math.Atan2(-m[0, 1], m[0, 0]);
			v.z = 0;
		}

		for (var i = 0; i < v.Size; i++)
		{
			if (v[i] < 0)
			{
				v[i] += 2 * Math.PI;
			}
			else if (v[i] > 2 * Math.PI)
			{
				v[i] -= 2 * Math.PI;
			}
		}

		return v;
	}

	public static Matrix4x4d QuaternionToMatrix(in Quaterniond quat)
	{
		var m = new Matrix4x4d();

		var x = quat.x * 2;
		var y = quat.y * 2;
		var z = quat.z * 2;
		var xx = quat.x * x;
		var yy = quat.y * y;
		var zz = quat.z * z;
		var xy = quat.x * y;
		var xz = quat.x * z;
		var yz = quat.y * z;
		var wx = quat.w * x;
		var wy = quat.w * y;
		var wz = quat.w * z;

		m[0] = 1.0f - (yy + zz);
		m[1] = xy + wz;
		m[2] = xz - wy;
		m[3] = 0.0F;

		m[4] = xy - wz;
		m[5] = 1.0f - (xx + zz);
		m[6] = yz + wx;
		m[7] = 0.0F;

		m[8] = xz + wy;
		m[9] = yz - wx;
		m[10] = 1.0f - (xx + yy);
		m[11] = 0.0F;

		m[12] = 0.0F;
		m[13] = 0.0F;
		m[14] = 0.0F;
		m[15] = 1.0F;

		return m;
	}

	private static Quaterniond MatrixToQuaternion(in Matrix4x4d m)
	{
		var quat = new Quaterniond();

		var fTrace = m[0, 0] + m[1, 1] + m[2, 2];
		double root;

		if (fTrace > 0)
		{
			root = Math.Sqrt(fTrace + 1);
			quat.w = 0.5D * root;
			root = 0.5D / root;
			quat.x = (m[2, 1] - m[1, 2]) * root;
			quat.y = (m[0, 2] - m[2, 0]) * root;
			quat.z = (m[1, 0] - m[0, 1]) * root;
		}
		else
		{
			var s_iNext = new int[] { 1, 2, 0 };

			int i = 0;
			if (m[1, 1] > m[0, 0])
			{
				i = 1;
			}
			if (m[2, 2] > m[i, i])
			{
				i = 2;
			}

			var j = s_iNext[i];
			var k = s_iNext[j];

			root = Math.Sqrt(m[i, i] - m[j, j] - m[k, k] + 1);
			if (root < 0)
			{
				throw new IndexOutOfRangeException("error!");
			}
			quat[i] = 0.5 * root;
			root = 0.5f / root;
			quat.w = (m[k, j] - m[j, k]) * root;
			quat[j] = (m[j, i] + m[i, j]) * root;
			quat[k] = (m[k, i] + m[i, k]) * root;
		}

		var nor = Math.Sqrt(Dot(quat, quat));

		quat.Set(quat.x / nor, quat.y / nor, quat.z / nor, quat.w / nor);

		return quat;
	}

	private static Matrix4x4d LookRotationToMatrix(in Vector3d viewVec, in Vector3d upVec)
	{
		var z = viewVec;
		var m = new Matrix4x4d();

		var mag = Vector3d.Magnitude(z);
		if (mag < 0)
		{
			m = Matrix4x4d.identity;
		}
		z /= mag;

		var x = Vector3d.Cross(upVec, z);
		mag = Vector3d.Magnitude(x);
		if (mag < 0)
		{
			m = Matrix4x4d.identity;
		}
		x /= mag;

		var y = Vector3d.Cross(z, x);

		m[0, 0] = x.x; m[0, 1] = y.x; m[0, 2] = z.x;
		m[1, 0] = x.y; m[1, 1] = y.y; m[1, 2] = z.y;
		m[2, 0] = x.z; m[2, 1] = y.z; m[2, 2] = z.z;

		return m;
	}

	public static Quaterniond operator *(in Quaterniond lhs, in Quaterniond rhs)
	{
		return new Quaterniond(lhs.w * rhs.x + lhs.x * rhs.w + lhs.y * rhs.z - lhs.z * rhs.y,
							   lhs.w * rhs.y + lhs.y * rhs.w + lhs.z * rhs.x - lhs.x * rhs.z,
							   lhs.w * rhs.z + lhs.z * rhs.w + lhs.x * rhs.y - lhs.y * rhs.x,
							   lhs.w * rhs.w - lhs.x * rhs.x - lhs.y * rhs.y - lhs.z * rhs.z);
	}

	public static Vector3d operator *(in Quaterniond rotation, in Vector3d point)
	{
		var num = rotation.x * 2;
		var num2 = rotation.y * 2;
		var num3 = rotation.z * 2;
		var num4 = rotation.x * num;
		var num5 = rotation.y * num2;
		var num6 = rotation.z * num3;
		var num7 = rotation.x * num2;
		var num8 = rotation.x * num3;
		var num9 = rotation.y * num3;
		var num10 = rotation.w * num;
		var num11 = rotation.w * num2;
		var num12 = rotation.w * num3;

		var result = Vector3d.zero;
		result.x = (1f - (num5 + num6)) * point.x + (num7 - num12) * point.y + (num8 + num11) * point.z;
		result.y = (num7 + num12) * point.x + (1f - (num4 + num6)) * point.y + (num9 - num10) * point.z;
		result.z = (num8 - num11) * point.x + (num9 + num10) * point.y + (1f - (num4 + num5)) * point.z;
		return result;
	}

	public static bool operator ==(in Quaterniond lhs, in Quaterniond rhs)
	{
		return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.w == rhs.w) ? true : false;
	}

	public static bool operator !=(in Quaterniond lhs, in Quaterniond rhs)
	{
		return !(lhs == rhs);
	}
}
