/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SDF
{
	public class Quaternion<T>
	{
		private TypeCode _code;
		private Vector3<T> _euler = new Vector3<T>();
		private T _w;
		private T _x;
		private T _y;
		private T _z;

		// x:roll, y:pitch, z:yaw

		public Quaternion(T w, T x, T y, T z)
		{
			SetTypeCode();
			Set(w, x, y, z);
		}

		public Quaternion()
			: this(default(T), default(T), default(T))
		{
		}

		public Quaternion(T roll, T pitch, T yaw)
		{
			SetTypeCode();
			Set(roll, pitch, yaw);
		}

		private void SetTypeCode()
		{
			_code = Type.GetTypeCode(typeof(T));
		}

		public T Roll
		{
			get => _euler.X;
			set {
				_euler.X = value;
				ConvertEuler2Quaternion();
			}
		}

		public T Pitch
		{
			get => _euler.Y;
			set {
				_euler.Y = value;
				ConvertEuler2Quaternion();
			}
		}

		public T Yaw
		{
			get => _euler.Z;
			set {
				_euler.Z = value;
				ConvertEuler2Quaternion();
			}
		}

		public T W
		{
			get => _w;
			set {
				_w = value;
				ConvertQuaternion2Euler();
			}
		}

		public T X
		{
			get => _x;
			set {
				_x = value;
				ConvertQuaternion2Euler();
			}
		}

		public T Y
		{
			get => _y;
			set {
				_y = value;
				ConvertQuaternion2Euler();
			}
		}

		public T Z
		{
			get => _z;
			set {
				_z = value;
				ConvertQuaternion2Euler();
			}
		}

		public void Set(T roll, T pitch, T yaw)
		{
			_euler.Set(roll, pitch, yaw);

			ConvertEuler2Quaternion();
		}

		public void Set(T w, T x, T y, T z)
		{
			_w = w;
			_x = x;
			_y = y;
			_z = z;

			ConvertQuaternion2Euler();
		}

		public void Set(in string roll, in string pitch, in string yaw)
		{
			Set((T)Convert.ChangeType(roll, _code),
				(T)Convert.ChangeType(pitch, _code),
				(T)Convert.ChangeType(yaw, _code));
		}

		public void Set(in string w, in string x, in string y, in string z)
		{
			Set((T)Convert.ChangeType(w, _code),
				(T)Convert.ChangeType(x, _code),
				(T)Convert.ChangeType(y, _code),
				(T)Convert.ChangeType(z, _code));
		}

		private void ConvertEuler2Quaternion()
		{
			var roll = Convert.ToDouble(_euler.X);
			var pitch = Convert.ToDouble(_euler.Y);
			var yaw = Convert.ToDouble(_euler.Z);
			var phi = roll * 0.5d;
			var the = pitch * 0.5d;
			var psi = yaw * 0.5d;

			_w = (T)(dynamic)(Math.Cos(phi) * Math.Cos(the) * Math.Cos(psi) +
							  Math.Sin(phi) * Math.Sin(the) * Math.Sin(psi));

			_x = (T)(dynamic)(Math.Sin(phi) * Math.Cos(the) * Math.Cos(psi) -
							  Math.Cos(phi) * Math.Sin(the) * Math.Sin(psi));

			_y = (T)(dynamic)(Math.Cos(phi) * Math.Sin(the) * Math.Cos(psi) +
							  Math.Sin(phi) * Math.Cos(the) * Math.Sin(psi));

			_z = (T)(dynamic)(Math.Cos(phi) * Math.Cos(the) * Math.Sin(psi) -
							  Math.Sin(phi) * Math.Sin(the) * Math.Cos(psi));

			Normalize();
		}

		private void Normalize()
		{
			var w = Convert.ToDouble(_w);
			var x = Convert.ToDouble(_x);
			var y = Convert.ToDouble(_y);
			var z = Convert.ToDouble(_z);

			var s = Math.Sqrt(w * w + x * x + y * y + z * z);

			if (Math.Equals(s, 0d))
			{
				_w = (T)(object)(1.0d);
				_x = (T)(object)(0.0d);
				_y = (T)(object)(0.0d);
				_z = (T)(object)(0.0d);
			}
			else
			{
				_w = (T)(object)(w / s);
				_x = (T)(object)(x / s);
				_y = (T)(object)(y / s);
				_z = (T)(object)(z / s);
			}
		}

		private void ConvertQuaternion2Euler()
		{
			const double tol = 1e-15d;

			Normalize();

			var w = Convert.ToDouble(_w);
			var x = Convert.ToDouble(_x);
			var y = Convert.ToDouble(_y);
			var z = Convert.ToDouble(_z);

			var squ = w * w;
			var sqx = x * x;
			var sqy = y * y;
			var sqz = z * z;

			// Pitch
			var sarg = -2 * (x * z - w * y);
			if (sarg <= -1.0d)
			{
				_euler.Y = (T)(dynamic)(-0.5d * Math.PI);
			}
			else if (sarg >= 1.0d)
			{
				_euler.Y = (T)(dynamic)(0.5d * Math.PI);
			}
			else
			{
				_euler.Y = (T)(dynamic)(Math.Asin(sarg));
			}

			// If the pitch angle is PI/2 or -PI/2, we can only compute the sum roll + yaw.  However, any combination that gives
			// the right sum will produce the correct orientation, so we  set yaw = 0 and compute roll.
			// pitch angle is PI/2
			if (Math.Abs(sarg - 1) < tol)
			{
				// Roll
				_euler.X = (T)(dynamic)Math.Atan2(2 * (x * y - z * w),
												  squ - sqx + sqy - sqz);

				// Yaw
				_euler.Z = (T)(dynamic)0d;
			}
			// pitch angle is -PI/2
			else if (Math.Abs(sarg + 1) < tol)
			{
				// Roll
				_euler.X = (T)(dynamic)Math.Atan2(-2 * (x * y - z * w),
												  squ - sqx + sqy - sqz);

				// Yaw
				_euler.Z = (T)(dynamic)0d;
			}
			else
			{
				// Roll
				_euler.X = (T)(dynamic)Math.Atan2(2 * (y * z + w * x),
												  squ - sqx - sqy + sqz);

				// Yaw
				_euler.Z = (T)(dynamic)Math.Atan2(2 * (x * y + w * z),
												  squ + sqx - sqy - sqz);
			}
		}

		public void FromString(in string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			var tmp = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (tmp.Length == 3)
			{
				Set(tmp[0], tmp[1], tmp[2]);
			}
			else if (tmp.Length == 4)
			{
				Set(tmp[0], tmp[1], tmp[2], tmp[3]);
			}
		}

		public override string ToString()
		{
			// return $"Quaternion({_w}, {_x}, {_y}, {_z})";
			return $"{Roll:f10} {Pitch:f10} {Yaw:f10}";
		}
	}
}