/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SDF
{
	public class Vector2<T>
	{
		private T x;
		private T y;

		public Vector2()
			: this(default(T), default(T))
		{
		}

		public Vector2(T _x, T _y)
		{
			x = _x;
			y = _y;
		}

		public T X
		{
			get => x;
			set => x = value;
		}

		public T Y
		{
			get => y;
			set => y = value;
		}

		public void Set(T _x, T _y)
		{
			X = _x;
			Y = _y;
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
				var code = Type.GetTypeCode(typeof(T));
				if (code != TypeCode.Empty)
				{
					X = (T)Convert.ChangeType(tmp[0], code);
					Y = (T)Convert.ChangeType(tmp[1], code);
				}
			}
		}
	}

	public class Vector3<T>
	{
		private Vector2<T> xy;
		private T z;

		public Vector3()
			: this(default(T), default(T), default(T))
		{
		}

		public Vector3(T _x, T _y, T _z)
		{
			xy = new Vector2<T>(_x, _y);
			z = _z;
		}

		public T X
		{
			get => xy.X;
			set => xy.X = value;
		}

		public T Y
		{
			get => xy.Y;
			set => xy.Y = value;
		}

		public T Z
		{
			get => z;
			set => z = value;
		}

		public void Set(T _x, T _y, T _z)
		{
			xy.Set(_x, _y);
			Z = _z;
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
				var code = Type.GetTypeCode(typeof(T));
				if (code != TypeCode.Empty)
				{
					X = (T)Convert.ChangeType(tmp[0], code);
					Y = (T)Convert.ChangeType(tmp[1], code);
					Z = (T)Convert.ChangeType(tmp[2], code);
				}
			}
		}
	}

	public class Quaternion<T>
	{
		// private T x;
		// private T y;
		// private T z;
		// private T w;
		private T roll;
		private T pitch;
		private T yaw;

		// public Quaternion(T _qx, T _qy, T _qz, T _qw)
		// {
		// 	x = _qx;
		// 	y = _qy;
		// 	z = _qz;
		// 	w = _qw;
		//  roll = arctan(2*(x*y + z*w), 1 - 2*(y^2 * z^2));
		//  pitch = arcsin(2*(x*z - w*y));
		//  yaw = arctan(2*(x*w + y*z), 1 - 2*(y^2 * z^2));
		// }

		public Quaternion()
			: this(default(T), default(T), default(T))
		{
		}

		public Quaternion(T _roll, T _pitch, T _yaw)
		{
			roll = _roll;
			pitch = _pitch;
			yaw = _yaw;
		}

		public T Roll
		{
			get => roll;
			set => roll = value;
		}

		public T Pitch
		{
			get => pitch;
			set => pitch = value;
		}
		public T Yaw
		{
			get => yaw;
			set => yaw = value;
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
				var code = Type.GetTypeCode(typeof(T));

				if (code != TypeCode.Empty)
				{
					roll = (T)Convert.ChangeType(tmp[0], code);
					pitch = (T)Convert.ChangeType(tmp[1], code);
					yaw = (T)Convert.ChangeType(tmp[2], code);
				}
			}
		}
	}

	public class Pose<T>
	{
		private Vector3<T> pos;
		private Quaternion<T> rot;

		public Pose()
			: this(default(T), default(T), default(T))
		{
		}

		public Pose(T _x, T _y, T _z)
			: this(_x, _y, _z, default(T), default(T), default(T))
		{
		}

		public Pose(T _x, T _y, T _z, T _roll, T _pitch, T _yaw)
		{
			pos = new Vector3<T>(_x, _y, _z);
			rot = new Quaternion<T>(_roll, _pitch, _yaw);
		}

		public Vector3<T> Pos
		{
			get => pos;
			set => pos = value;
		}

		public Quaternion<T> Rot
		{
			get => rot;
			set => rot = value;
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
				pos.FromString(tmp[0] + " " + tmp[1] + " " + tmp[2]);
				rot.FromString(tmp[3] + " " + tmp[4] + " " + tmp[5]);
			}
		}

		// public Pose(T _x, T _y, T _z, T _qx, T _qy, T _qz, T _qw)
		// {
		// 	pos = new Vector<T>(_x, _y, _z);
		// 	rot = new Quaternion<T>(_qx, _qy, _qz, _qw);
		// }
	}
}