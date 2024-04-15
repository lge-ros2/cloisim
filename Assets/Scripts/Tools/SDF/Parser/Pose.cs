/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

namespace SDF
{
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

		public Pose(in T x, in T y, in T z)
			: this(x, y, z, default(T), default(T), default(T))
		{
		}

		public Pose(in T qw, in T qx, in T qy, in T qz)
			: this(default(T), default(T), default(T), qw, qx, qy, qz)
		{
		}

		public Pose(in T x, in T y, in T z, in T roll, in T pitch, in T yaw)
		{
			_pos = new Vector3<T>(x, y, z);
			_rot = new Quaternion<T>(roll, pitch, yaw);
		}

		public Pose(in T x, in T y, in T z, in T qw, in T qx, in T qy, in T qz)
		{
			_pos = new Vector3<T>(x, y, z);
			_rot = new Quaternion<T>(qw, qx, qy, qz);
		}

		public Pose(in Vector3<T> vec, in Quaternion<T> rot)
		{
			_pos = vec;
			_rot = rot;
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
			// return $"Pose({_pos.ToString()}, {_rot.ToString()})";
			return $"{_pos.ToString()} {_rot.ToString()}";
		}
	}
}