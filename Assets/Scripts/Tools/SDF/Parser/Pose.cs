/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text.RegularExpressions;
using System.Globalization;
using System;

namespace SDF
{
	public class Vector2<T>
	{
		private static string regex_num_pattern = "[^.0-9Ee+-]";
		protected static readonly Regex regex_num = new Regex(regex_num_pattern);

		protected T _x;
		protected T _y;

		public Vector2()
			: this(default(T), default(T))
		{
		}

		public Vector2(in string value)
		{
			FromString(value);
		}

		public Vector2(T x, T y)
		{
			_x = x;
			_y = y;
		}

		public T X
		{
			get => _x;
			set => _x = value;
		}

		public T Y
		{
			get => _y;
			set => _y = value;
		}

		public void Set(T x, T y)
		{
			_x = x;
			_y = y;
			// Console.WriteLine(typeof(T) + "::" + _x + ", " + _y);
		}

		public void Set(string x, string y)
		{
			var code = Type.GetTypeCode(typeof(T));
			if (code != TypeCode.Empty)
			{
				x = regex_num.Replace(x, string.Empty);
				y = regex_num.Replace(y, string.Empty);
				var parsed_x = double.Parse(x, NumberStyles.Float);
				var parsed_y = double.Parse(y, NumberStyles.Float);
				// Console.WriteLine(typeof(T) + "::" + parsed_x + ", " + parsed_y);
				Set((T)Convert.ChangeType(parsed_x, code), (T)Convert.ChangeType(parsed_y, code));
			}
		}

		public void FromString(in string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			var tmp = value.Trim().Split(' ');
			if (tmp.Length == 2)
			{
				Set(tmp[0], tmp[1]);
			}
		}

		public override string ToString()
		{
			return $"Vector2({_x}, {_y})";
		}
	}

	public class Vector3<T> : Vector2<T>
	{
		private T _z;

		public Vector3()
			: this(default(T), default(T), default(T))
		{
		}

		public Vector3(in string value)
		{
			FromString(value);
		}

		public Vector3(T x, T y, T z)
		{
			Set(x, y, z);
		}

		public T Z
		{
			get => _z;
			set => _z = value;
		}

		public void Set(T x, T y, T z)
		{
			base.Set(x, y);
			_z = z;
			// Console.WriteLine(typeof(T) + "::" + _z);
		}

		public void Set(string x, string y, string z)
		{
			base.Set(x, y);

			var code = Type.GetTypeCode(typeof(T));
			if (!code.Equals(TypeCode.Empty))
			{
				z = regex_num.Replace(z, string.Empty);
				var parsed_z = double.Parse(z, NumberStyles.Float);
				// Console.WriteLine(typeof(T) + "::" + parsed_z);
				_z = (T)Convert.ChangeType(parsed_z, code);
			}
		}

		new public void FromString(in string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			var tmp = value.Trim().Split(' ');
			if (tmp.Length == 3)
			{
				Set(tmp[0], tmp[1], tmp[2]);
			}
		}

		public override string ToString()
		{
			return $"Vector3({_x}, {_y}, {_z})";
		}
	}

	public class Quaternion<T>
	{
		private T _w;
		private T _x;
		private T _y;
		private T _z;

		// x:roll, y:pitch, z:yaw
		private Vector3<T> euler = new Vector3<T>();

		public Quaternion(T w, T x, T y, T z)
		{
			Set(w, x, y, z);
		}

		public Quaternion()
			: this(default(T), default(T), default(T))
		{
		}

		public Quaternion(T roll, T pitch, T yaw)
		{
			Set(roll, pitch, yaw);
		}

		public T Roll
		{
			get => euler.X;
			set => euler.X = value;
		}

		public T Pitch
		{
			get => euler.Y;
			set => euler.Y = value;
		}

		public T Yaw
		{
			get => euler.Z;
			set => euler.Z = value;
		}

		public T W
		{
			get => _w;
			set => _w = value;
		}

		public T X
		{
			get => _x;
			set => _x = value;
		}

		public T Y
		{
			get => _y;
			set => _y = value;
		}

		public T Z
		{
			get => _z;
			set => _z = value;
		}

		public void Set(T roll, T pitch, T yaw)
		{
			euler.Set(roll, pitch, yaw);

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
			var code = Type.GetTypeCode(typeof(T));
			if (code != TypeCode.Empty)
			{
				Set((T)Convert.ChangeType(roll, code), (T)Convert.ChangeType(pitch, code), (T)Convert.ChangeType(yaw, code));
			}
		}

		public void Set(in string w, in string x, in string y, in string z)
		{
			var code = Type.GetTypeCode(typeof(T));
			if (code != TypeCode.Empty)
			{
				Set((T)Convert.ChangeType(w, code), (T)Convert.ChangeType(x, code), (T)Convert.ChangeType(y, code), (T)Convert.ChangeType(z, code));
			}
		}

		public void FromString(in string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			var tmp = value.Trim().Split(' ');

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
			return $"Quaternion({_w}, {_x}, {_y}, {_z})";
		}

		private void ConvertEuler2Quaternion()
		{
			var roll = Convert.ToDouble(euler.X);
			var pitch = Convert.ToDouble(euler.Y);
			var yaw = Convert.ToDouble(euler.Z);
			var phi = roll / 2.0;
			var the = pitch / 2.0;
			var psi = yaw / 2.0;

			_w = (T)(object)(Math.Cos(phi) * Math.Cos(the) * Math.Cos(psi) + Math.Sin(phi) * Math.Sin(the) * Math.Sin(psi));
			_x = (T)(object)(Math.Sin(phi) * Math.Cos(the) * Math.Cos(psi) - Math.Cos(phi) * Math.Sin(the) * Math.Sin(psi));
			_y = (T)(object)(Math.Cos(phi) * Math.Sin(the) * Math.Cos(psi) + Math.Sin(phi) * Math.Cos(the) * Math.Sin(psi));
			_z = (T)(object)(Math.Cos(phi) * Math.Cos(the) * Math.Sin(psi) - Math.Sin(phi) * Math.Sin(the) * Math.Cos(psi));

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
				euler.Y = (T)(object)(-0.5d * Math.PI);
			}
			else if (sarg >= 1.0d)
			{
				euler.Y = (T)(object)(0.5d * Math.PI);
			}
			else
			{
				euler.Y = (T)(object)(Math.Asin(sarg));
			}

			// If the pitch angle is PI/2 or -PI/2, we can only compute the sum roll + yaw.  However, any combination that gives
			// the right sum will produce the correct orientation, so we  set yaw = 0 and compute roll.
			// pitch angle is PI/2
			if (Math.Abs(sarg - 1) < tol)
			{
				euler.Z = (T)(object)0d;
				euler.X = (T)(object)Math.Atan2(2 * x * y - z * w, squ - sqx + sqy - sqz);
			}
			// pitch angle is -PI/2
			else if (Math.Abs(sarg + 1) < tol)
			{
				euler.Z = (T)(object)0d;
				euler.X = (T)(object)Math.Atan2(-2 * x * y - z * w, squ - sqx + sqy - sqz);
			}
			else
			{
				// Roll
				euler.X = (T)(object)Math.Atan2(2 * y * z + w * x, squ - sqx - sqy + sqz);

				// Yaw
				euler.Z = (T)(object)Math.Atan2(2 * x * y + w * z, squ + sqx - sqy - sqz);
			}
		}
	}

	public class Pose<T>
	{
		private Vector3<T> _pos;
		private Quaternion<T> _rot;

		public string relative_to = string.Empty; // TBD : since SDF 1.7

		#region SDF 1.9 feature
		// Description: 'euler_rpy' by default.
		// Supported rotation formats are 'euler_rpy', Euler angles representation in roll, pitch, yaw.
		// The pose is expected to have 6 values.
		// 'quat_xyzw', Quaternion representation in x, y, z, w. The pose is expected to have 7 values.
		public string rotation_format = "euler_rpy";

		// Description: Whether or not the euler angles are in degrees, otherwise they will be interpreted as radians by default.
		public bool degrees = false;
		#endregion

		public Pose()
			: this(default(T), default(T), default(T))
		{
		}

		public Pose(T x, T y, T z)
			: this(x, y, z, default(T), default(T), default(T))
		{
		}

		public Pose(T qw, T qx, T qy, T qz)
			: this(default(T), default(T), default(T), qw, qx, qy, qz)
		{
		}

		public Pose(T x, T y, T z, T roll, T pitch, T yaw)
		{
			_pos = new Vector3<T>(x, y, z);
			_rot = new Quaternion<T>(roll, pitch, yaw);
		}

		public Pose(T x, T y, T z, T qw, T qx, T qy, T qz)
		{
			_pos = new Vector3<T>(x, y, z);
			_rot = new Quaternion<T>(qw, qx, qy, qz);
		}

		public Vector3<T> Pos
		{
			get => _pos;
			set => _pos = value;
		}

		public Quaternion<T> Rot
		{
			get => _rot;
			set => _rot = value;
		}

		public void FromString(in string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return;
			}

			var tmp = value.Trim().Split(' ');
			if (tmp.Length == 6)
			{
				_pos.Set(tmp[0], tmp[1], tmp[2]);
				_rot.Set(tmp[3], tmp[4], tmp[5]);
			}
		}

		public override string ToString()
		{
			return $"Pose({_pos.ToString()}, {_rot.ToString()})";
		}
	}
}