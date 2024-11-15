/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;

public struct Matrix4x4d
{
	public double m00;
	public double m01;
	public double m02;
	public double m03;
	public double m10;
	public double m11;
	public double m12;
	public double m13;
	public double m20;
	public double m21;
	public double m22;
	public double m23;
	public double m30;
	public double m31;
	public double m32;
	public double m33;

	public double this[in int index]
	{
		get
		{
			switch (index)
			{
				case 0:
					return m00;
				case 1:
					return m10;
				case 2:
					return m20;
				case 3:
					return m30;
				case 4:
					return m01;
				case 5:
					return m11;
				case 6:
					return m21;
				case 7:
					return m31;
				case 8:
					return m02;
				case 9:
					return m12;
				case 10:
					return m22;
				case 11:
					return m32;
				case 12:
					return m03;
				case 13:
					return m13;
				case 14:
					return m23;
				case 15:
					return m33;
				default:
					throw new IndexOutOfRangeException("Invalid matrixd index!");
			}
		}
		set
		{
			switch (index)
			{
				case 0:
					m00 = value;
					break;
				case 1:
					m10 = value;
					break;
				case 2:
					m20 = value;
					break;
				case 3:
					m30 = value;
					break;
				case 4:
					m01 = value;
					break;
				case 5:
					m11 = value;
					break;
				case 6:
					m21 = value;
					break;
				case 7:
					m31 = value;
					break;
				case 8:
					m02 = value;
					break;
				case 9:
					m12 = value;
					break;
				case 10:
					m22 = value;
					break;
				case 11:
					m32 = value;
					break;
				case 12:
					m03 = value;
					break;
				case 13:
					m13 = value;
					break;
				case 14:
					m23 = value;
					break;
				case 15:
					m33 = value;
					break;
				default:
					throw new IndexOutOfRangeException("Invalid matrixd index!");
			}
		}
	}
	public double this[in int row, in int column]
	{
		get
		{
			return this[row + column * 4];
		}
		set
		{
			this[row + column * 4] = value;
		}
	}

	public static Matrix4x4d identity
	{
		get
		{
			var temp = new Matrix4x4d();
			temp[0, 0] = 1;
			temp[1, 1] = 1;
			temp[2, 2] = 1;
			temp[3, 3] = 1;

			return temp;
		}
	}

	public static Matrix4x4d zero => new Matrix4x4d();

	public double determinant => (
				m03 * m12 * m21 * m30 - m02 * m13 * m21 * m30 - m03 * m11 * m22 * m30 + m01 * m13 * m22 * m30 +
				m02 * m11 * m23 * m30 - m01 * m12 * m23 * m30 - m03 * m12 * m20 * m31 + m02 * m13 * m20 * m31 +
				m03 * m10 * m22 * m31 - m00 * m13 * m22 * m31 - m02 * m10 * m23 * m31 + m00 * m12 * m23 * m31 +
				m03 * m11 * m20 * m32 - m01 * m13 * m20 * m32 - m03 * m10 * m21 * m32 + m00 * m13 * m21 * m32 +
				m01 * m10 * m23 * m32 - m00 * m11 * m23 * m32 - m02 * m11 * m20 * m33 + m01 * m12 * m20 * m33 +
				m02 * m10 * m21 * m33 - m00 * m12 * m21 * m33 - m01 * m10 * m22 * m33 + m00 * m11 * m22 * m33);

	public Matrix4x4d inverse
	{
		get
		{
			const int m = 4;
			const int n = 4;
			double[,] array = new double[2 * m + 1, 2 * n + 1];

			for (var i = 0; i < m; i++)
			{
				for (var j = 0; j < n; j++)
				{
					array[i, j] = this[i, j];
				}
			}

			for (var k = 0; k < m; k++)
			{
				for (var t = n; t <= 2 * n; t++)
				{
					if ((t - k) == m)
					{
						array[k, t] = 1.0;
					}
					else
					{
						array[k, t] = 0;
					}
				}
			}

			for (var k = 0; k < m; k++)
			{
				if (array[k, k] != 1)
				{
					var bs = array[k, k];
					array[k, k] = 1;
					for (var p = k + 1; p < 2 * n; p++)
					{
						array[k, p] /= bs;
					}
				}

				for (var q = 0; q < m; q++)
				{
					if (q != k)
					{
						var bs = array[q, k];
						for (var p = 0; p < 2 * n; p++)
						{
							array[q, p] -= bs * array[k, p];
						}
					}
					else
					{
						continue;
					}
				}
			}

			var result = new Matrix4x4d();
			for (var x = 0; x < m; x++)
			{
				for (var y = n; y < 2 * n; y++)
				{
					result[x, y - n] = array[x, y];
				}
			}
			return result;
		}
	}

	public bool IsIdentity => (this == identity);

	public Matrix4x4d transpose
	{
		get
		{
			var temp = new Matrix4x4d();
			temp.m00 = m00; temp.m10 = m01; temp.m20 = m02; temp.m30 = m03;
			temp.m01 = m10; temp.m11 = m11; temp.m21 = m12; temp.m31 = m13;
			temp.m02 = m20; temp.m12 = m21; temp.m22 = m22; temp.m32 = m23;
			temp.m03 = m30; temp.m13 = m31; temp.m23 = m32; temp.m33 = m33;
			return temp;
		}
	}

	public static double Determinant(in Matrix4x4d m)
	{
		return m.determinant;
	}

	public static Matrix4x4d Inverse(in Matrix4x4d m)
	{
		return m.inverse;
	}

	public static Matrix4x4d LookAt(Vector3d from, Vector3d to, Vector3d up)
	{
		throw new IndexOutOfRangeException("Not Available!");
	}

	public static Matrix4x4d Ortho(in double left, in double right, in double bottom, in double top, in double zNear, in double zFar)
	{
		var result = identity;

		var deltax = right - left;
		var deltay = top - bottom;
		var deltaz = zFar - zNear;

		result[0, 0] = 2 / deltax;
		result[0, 3] = -(right + left) / deltax;
		result[1, 1] = 2 / deltay;
		result[1, 3] = -(top + bottom) / deltay;
		result[2, 2] = -2 / deltaz;
		result[2, 3] = -(zFar + zNear) / deltaz;

		return result;
	}

	public static Matrix4x4d Perspective(in double fov, in double aspect, in double zNear, in double zFar)
	{
		var result = new Matrix4x4d();

		var radians = (fov / 2.0) * (Math.PI / 180);
		var cotangent = Math.Cos(radians) / Math.Sin(radians);
		var deltaZ = zNear - zFar;

		result[0, 0] = cotangent / aspect; result[0, 1] = 0; result[0, 2] = 0; result[0, 3] = 0;
		result[1, 0] = 0; result[1, 1] = cotangent; result[1, 2] = 0; result[1, 3] = 0;
		result[2, 0] = 0; result[2, 1] = 0; result[2, 2] = (zFar + zNear) / deltaZ; result[2, 3] = 2 * zNear * zFar / deltaZ;
		result[3, 0] = 0; result[3, 1] = 0; result[3, 2] = -1; result[3, 3] = 0;

		return result;
	}

	public static Matrix4x4d Scale(in Vector3d v)
	{
		Matrix4x4d result;
		result.m00 = v.x; result.m01 = 0; result.m02 = 0; result.m03 = 0;
		result.m10 = 0; result.m11 = v.y; result.m12 = 0; result.m13 = 0;
		result.m20 = 0; result.m21 = 0; result.m22 = v.z; result.m23 = 0;
		result.m30 = 0; result.m31 = 0; result.m32 = 0; result.m33 = 1;
		return result;
	}

	public static Matrix4x4d Translate(in Vector3d v)
	{
		Matrix4x4d result;
		result.m00 = 1; result.m01 = 0; result.m02 = 0; result.m03 = v.x;
		result.m10 = 0; result.m11 = 1; result.m12 = 0; result.m13 = v.y;
		result.m20 = 0; result.m21 = 0; result.m22 = 1; result.m23 = v.z;
		result.m30 = 0; result.m31 = 0; result.m32 = 0; result.m33 = 1;
		return result;
	}

	public static Matrix4x4d Transpose(in Matrix4x4d m)
	{
		return m.transpose;
	}

	public static Matrix4x4d TRS(in Vector3d pos, in Quaterniond q, in Vector3d s)
	{
		var m = Quaterniond.QuaternionToMatrix(q);

		m[0] *= s[0];
		m[1] *= s[0];
		m[2] *= s[0];

		m[4] *= s[1];
		m[5] *= s[1];
		m[6] *= s[1];

		m[8] *= s[2];
		m[9] *= s[2];
		m[10] *= s[2];

		m[12] = pos[0];
		m[13] = pos[1];
		m[14] = pos[2];
		return m;
	}

	public Vector4d GetColumn(in int i)
	{
		Vector4d result;
		switch (i)
		{
			case 0:
				result = new Vector4d(m00, m10, m20, m30);
				break;
			case 1:
				result = new Vector4d(m01, m11, m21, m31);
				break;
			case 2:
				result = new Vector4d(m02, m12, m22, m32);
				break;
			case 3:
				result = new Vector4d(m03, m13, m23, m33);
				break;
			default:
				throw new IndexOutOfRangeException("Invalid column index!");
		}
		return result;
	}

	public Vector4d GetRow(in int i)
	{
		Vector4d result;
		switch (i)
		{
			case 0:
				result = new Vector4d(m00, m01, m02, m03);
				break;
			case 1:
				result = new Vector4d(m10, m11, m12, m13);
				break;
			case 2:
				result = new Vector4d(m20, m21, m22, m23);
				break;
			case 3:
				result = new Vector4d(m30, m31, m32, m33);
				break;
			default:
				throw new IndexOutOfRangeException("Invalid row index!");
		}
		return result;

	}

	public Vector3d MultiplyPoint(in Vector3d v)
	{
		var result = Vector3d.zero;
		result.x = m00 * v.x + m01 * v.y + m02 * v.z + m03;
		result.y = m10 * v.x + m11 * v.y + m12 * v.z + m13;
		result.z = m20 * v.x + m21 * v.y + m22 * v.z + m23;

		var num = m30 * v.x + m31 * v.y + m32 * v.z + m33;
		num = 1 / num;
		result.x *= num;
		result.y *= num;
		result.z *= num;

		return result;
	}

	public Vector3d MultiplyPoint3x4(in Vector3d v)
	{
		var result = Vector3d.zero;
		result.x = m00 * v.x + m01 * v.y + m02 * v.z + m03;
		result.y = m10 * v.x + m11 * v.y + m12 * v.z + m13;
		result.z = m20 * v.x + m21 * v.y + m22 * v.z + m23;
		return result;
	}

	public Vector3d MultiplyVector(in Vector3 v)
	{
		return MultiplyVector(new Vector3d(v));
	}

	public Vector3d MultiplyVector(in Vector3d v)
	{
		var result = Vector3d.zero;
		result.x = m00 * v.x + m01 * v.y + m02 * v.z;
		result.y = m10 * v.x + m11 * v.y + m12 * v.z;
		result.z = m20 * v.x + m21 * v.y + m22 * v.z;
		return result;
	}

	public void SetColumn(in int i, in Vector4d v)
	{
		this[0, i] = v.x;
		this[1, i] = v.y;
		this[2, i] = v.z;
		this[3, i] = v.w;
	}

	public void SetRow(in int i, in Vector4d v)
	{
		this[i, 0] = v.x;
		this[i, 1] = v.y;
		this[i, 2] = v.z;
		this[i, 3] = v.w;
	}

	public void SetTRS(in Vector3d pos, in Quaterniond q, in Vector3d s)
	{
		this = TRS(pos, q, s);
	}

	public override string ToString()
	{
		return string.Format("{0}\t{1}\t{2}\t{3}\n{4}\t{5}\t{6}\t{7}\n{8}\t{9}\t{10}\t{11}\n{12}\t{13}\t{14}\t{15}",
							m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33);
	}

	public override int GetHashCode()
	{
		return GetColumn(0).GetHashCode() ^ GetColumn(1).GetHashCode() << 2 ^ GetColumn(2).GetHashCode() >> 2 ^ GetColumn(3).GetHashCode() >> 1;
	}

	public override bool Equals(object other)
	{
		return this == (Matrix4x4d)other;
	}

	public string ToString(in string format)
	{
		return string.Format("{0}\t{1}\t{2}\t{3}\n{4}\t{5}\t{6}\t{7}\n{8}\t{9}\t{10}\t{11}\n{12}\t{13}\t{14}\t{15}",
					m00.ToString(format), m01.ToString(format), m02.ToString(format), m03.ToString(format),
					m10.ToString(format), m11.ToString(format), m12.ToString(format), m13.ToString(format),
					m20.ToString(format), m21.ToString(format), m22.ToString(format), m23.ToString(format),
					m30.ToString(format), m31.ToString(format), m32.ToString(format), m33.ToString(format));
	}

	public static Vector4d operator *(in Matrix4x4d lhs, in Vector4d v)
	{
		var result = Vector4d.zero;
		result.x = lhs.m00 * v.x + lhs.m01 * v.y + lhs.m02 * v.z + lhs.m03 * v.w;
		result.y = lhs.m10 * v.x + lhs.m11 * v.y + lhs.m12 * v.z + lhs.m13 * v.w;
		result.z = lhs.m20 * v.x + lhs.m21 * v.y + lhs.m22 * v.z + lhs.m23 * v.w;
		result.w = lhs.m30 * v.x + lhs.m31 * v.y + lhs.m32 * v.z + lhs.m33 * v.w;
		return result;
	}

	public static Matrix4x4d operator *(in Matrix4x4d lhs, in Matrix4x4d rhs)
	{
		Matrix4x4d result;
		result.m00 = lhs.m00 * rhs.m00 + lhs.m01 * rhs.m10 + lhs.m02 * rhs.m20 + lhs.m03 * rhs.m30;
		result.m01 = lhs.m00 * rhs.m01 + lhs.m01 * rhs.m11 + lhs.m02 * rhs.m21 + lhs.m03 * rhs.m31;
		result.m02 = lhs.m00 * rhs.m02 + lhs.m01 * rhs.m12 + lhs.m02 * rhs.m22 + lhs.m03 * rhs.m32;
		result.m03 = lhs.m00 * rhs.m03 + lhs.m01 * rhs.m13 + lhs.m02 * rhs.m23 + lhs.m03 * rhs.m33;
		result.m10 = lhs.m10 * rhs.m00 + lhs.m11 * rhs.m10 + lhs.m12 * rhs.m20 + lhs.m13 * rhs.m30;
		result.m11 = lhs.m10 * rhs.m01 + lhs.m11 * rhs.m11 + lhs.m12 * rhs.m21 + lhs.m13 * rhs.m31;
		result.m12 = lhs.m10 * rhs.m02 + lhs.m11 * rhs.m12 + lhs.m12 * rhs.m22 + lhs.m13 * rhs.m32;
		result.m13 = lhs.m10 * rhs.m03 + lhs.m11 * rhs.m13 + lhs.m12 * rhs.m23 + lhs.m13 * rhs.m33;
		result.m20 = lhs.m20 * rhs.m00 + lhs.m21 * rhs.m10 + lhs.m22 * rhs.m20 + lhs.m23 * rhs.m30;
		result.m21 = lhs.m20 * rhs.m01 + lhs.m21 * rhs.m11 + lhs.m22 * rhs.m21 + lhs.m23 * rhs.m31;
		result.m22 = lhs.m20 * rhs.m02 + lhs.m21 * rhs.m12 + lhs.m22 * rhs.m22 + lhs.m23 * rhs.m32;
		result.m23 = lhs.m20 * rhs.m03 + lhs.m21 * rhs.m13 + lhs.m22 * rhs.m23 + lhs.m23 * rhs.m33;
		result.m30 = lhs.m30 * rhs.m00 + lhs.m31 * rhs.m10 + lhs.m32 * rhs.m20 + lhs.m33 * rhs.m30;
		result.m31 = lhs.m30 * rhs.m01 + lhs.m31 * rhs.m11 + lhs.m32 * rhs.m21 + lhs.m33 * rhs.m31;
		result.m32 = lhs.m30 * rhs.m02 + lhs.m31 * rhs.m12 + lhs.m32 * rhs.m22 + lhs.m33 * rhs.m32;
		result.m33 = lhs.m30 * rhs.m03 + lhs.m31 * rhs.m13 + lhs.m32 * rhs.m23 + lhs.m33 * rhs.m33;
		return result;
	}

	public static bool operator ==(in Matrix4x4d lhs, in Matrix4x4d rhs)
	{
		return lhs.GetColumn(0) == rhs.GetColumn(0) && lhs.GetColumn(1) == rhs.GetColumn(1) && lhs.GetColumn(2) == rhs.GetColumn(2) && lhs.GetColumn(3) == rhs.GetColumn(3);
	}

	public static bool operator !=(in Matrix4x4d lhs, in Matrix4x4d rhs)
	{
		return !(lhs == rhs);
	}
}