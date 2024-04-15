/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text.RegularExpressions;
using System.Globalization;
using System;

namespace SDF
{
	public class Vector
	{
		private static string regexNumPattern = "[^.0-9Ee+-]";
		protected static readonly Regex regex = new Regex(regexNumPattern);

		protected double StringToDouble(in string number)
		{
			var regexNumber = regex.Replace(number, string.Empty);
			return double.Parse(regexNumber, NumberStyles.Float);
		}
	}

	public class Vector2<T> : Vector
	{
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

		public Vector2(in T x, in T y)
		{
			this.Set(x, y);
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

		public void Set(in T x, in T y)
		{
			_x = x;
			_y = y;
			// Console.WriteLine(typeof(T) + "::" + _x + ", " + _y);
		}

		public void Set(in string x, in string y)
		{
			var code = Type.GetTypeCode(typeof(T));
			if (code != TypeCode.Empty)
			{
				var parsedX = StringToDouble(x);
				var parsedY = StringToDouble(y);
				// Console.WriteLine(typeof(T) + "::" + parsedX + ", " + parsedY);
				Set((T)Convert.ChangeType(parsedX, code), (T)Convert.ChangeType(parsedY, code));
			}
		}

		public static Vector2<T> operator +(in Vector2<T> v)
			=> v;

		public static Vector2<T> operator -(Vector2<T> v)
			=> new Vector2<T>(-(dynamic)v.X, -(dynamic)v.Y);

		public static Vector2<T> operator +(in Vector2<T> left, in Vector2<T> right)
			=> new Vector2<T>((dynamic)left.X + (dynamic)right.X, (dynamic)left.Y + (dynamic)right.Y);

		public static Vector2<T> operator -(in Vector2<T> left, in Vector2<T> right)
			=> left + (-right);

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
			// return $"Vector2({_x}, {_y})";
			return $"{_x} {_y}";
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

		public Vector3(in T x, in T y, in T z)
		{
			Set(x, y, z);
		}

		public T Z
		{
			get => _z;
			set => _z = value;
		}

		public void Set(in T x, in T y, in T z)
		{
			base.Set(x, y);
			_z = z;
			// Console.WriteLine(typeof(T) + "::" + _z);
		}

		public void Set(in string x, in string y, in string z)
		{
			var code = Type.GetTypeCode(typeof(T));
			if (!code.Equals(TypeCode.Empty))
			{
				var parsedZ = StringToDouble(z);
				// Console.WriteLine(typeof(T) + "::" + parsedZ);
				base.Set(x, y);
				_z = (T)Convert.ChangeType(parsedZ, code);
			}
		}

		public static Vector3<T> operator +(in Vector3<T> v)
			=> v;

		public static Vector3<T> operator -(Vector3<T> v)
			=> new Vector3<T>(-(dynamic)v.X, -(dynamic)v.Y, -(dynamic)v.Z);

		public static Vector3<T> operator +(in Vector3<T> left, in Vector3<T> right)
			=> new Vector3<T>(
				(dynamic)left.X + (dynamic)right.X,
				(dynamic)left.Y + (dynamic)right.Y,
				(dynamic)left.Z + (dynamic)right.Z);

		public static Vector3<T> operator -(in Vector3<T> left, in Vector3<T> right)
			=> left + (-right);


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
			// return $"Vector3({_x}, {_y}, {_z})";
			return $"{_x} {_y} {_z}";
		}
	}
}