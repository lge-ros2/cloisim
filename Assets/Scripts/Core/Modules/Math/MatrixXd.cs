/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public class MatrixXd
{
	private double[,] _elements = null;
	public readonly int Row;
	public readonly int Col;

	public MatrixXd(in int row, in int col)
	{
		this.Row = row;
		this.Col = col;

		_elements = new double[Row, Col];
	}

	public MatrixXd(in double[,] initialData)
		: this(initialData.GetLength(0), initialData.GetLength(1))
	{
		for (var i = 0; i < Row; i++)
		{
			for (var j = 0; j < Col; j++)
			{
				_elements[i, j] = initialData[i, j];
			}
		}
	}

	public MatrixXd(in MatrixXd mat)
		: this(mat.Row, mat.Col)
	{
		for (var i = 0; i < Row; i++)
		{
			for (var j = 0; j < Col; j++)
			{
				this[i, j] = mat[i, j];
			}
		}
	}

	public double this[int row, int col]
	{
		get
		{
			LengthCheck(row, col);
			return _elements[row, col];
		}
		set
		{
			LengthCheck(row, col);
			_elements[row, col] = value;
		}
	}

	private void LengthCheck(in int row, in int col)
	{
		if (row < 0 || row >= Row || col < 0 || col >= Col)
		{
			throw new IndexOutOfRangeException("Index is out of row range.");
		}
	}

	public override int GetHashCode()
	{
		var hashCode = 0;
		for (var i = 0; i < this.Row; i++)
		{
			for (var j = 0; j < this.Row; j++)
			{
				hashCode ^= this[i, j].GetHashCode();
			}
		}
		return hashCode;
	}

	public override bool Equals(object other)
	{
		return this == (MatrixXd)other;
	}

	public override string ToString()
	{
		return ToString("G");
	}

	public string ToString(in string format)
	{
		var str = "";
		for (var i = 0; i < Row; i++)
		{
			str += "[";
			for (var j = 0; j < Col; j++)
			{
				str += this[i, j].ToString(format) + (j == this.Col - 1 ? "" : ", ");
			}
			str += "]" + (i == this.Row - 1 ? "" : (", " + System.Environment.NewLine));
		}
		return str;
	}


	public MatrixXd Inverse
	{
		get
		{
			var m = this.Row;
			var n = this.Col;
			var array = new double[2 * m + 1, 2 * n + 1];

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
					array[k, t] = ((t - k) == m) ? 1.0 : 0;
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
					if (q == k)
					{
						continue;
					}

					var bs = array[q, k];
					for (var p = 0; p < 2 * n; p++)
					{
						array[q, p] -= bs * array[k, p];
					}
				}
			}

			var result = new MatrixXd(m, n);
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

	private static MatrixXd Add(in MatrixXd lhs, in MatrixXd rhs, in double sign)
	{
		if (lhs.Row != rhs.Row || lhs.Col != rhs.Col)
		{
			throw new IndexOutOfRangeException($"Mismatch MatrixXd LHS({lhs.Row},{lhs.Col}) RHS({rhs.Row},{rhs.Col}) Size!!");
		}

		var result = new MatrixXd(lhs.Row, rhs.Col);

		for (var i = 0; i < lhs.Row; i++)
		{
			for (var j = 0; j < rhs.Col; j++)
			{
				result[i, j] = lhs[i, j] + sign * rhs[i, j];
			}
		}

		return result;
	}

	public static MatrixXd operator -(in MatrixXd a)
	{
		var result = new MatrixXd(a);

		for (var i = 0; i < result.Row; i++)
		{
			for (var j = 0; j < result.Col; j++)
			{
				result[i, j] = -result[i, j];
			}
		}

		return result;
	}

	public static MatrixXd operator +(in MatrixXd lhs, in MatrixXd rhs)
	{
		return Add(lhs, rhs, 1);
	}

	public static MatrixXd operator -(in MatrixXd lhs, in MatrixXd rhs)
	{
		return Add(lhs, rhs, -1);
	}

	public static MatrixXd operator *(in MatrixXd lhs, in double rhs)
	{
		var result = new MatrixXd(lhs);

		for (var i = 0; i < result.Row; i++)
		{
			for (var j = 0; j < result.Col; j++)
			{
				result[i, j] = result[i, j] * rhs;
			}
		}

		return result;
	}

	public static MatrixXd operator *(in double lhs, in MatrixXd rhs)
	{
		return rhs * lhs;
	}

	public static VectorXd operator *(in MatrixXd lhs, in Vector2d rhs)
	{
		if (lhs.Col != rhs.Size)
		{
			throw new IndexOutOfRangeException("Mismatch MatrixXd colume size!");
		}

		var result = new VectorXd(lhs.Row);

		for (var i = 0; i < lhs.Row; i++)
		{
			result[i] = lhs[i, 0] * rhs.x + lhs[i, 1] * rhs.y;
		}

		return result;
	}

	public static VectorXd operator *(in MatrixXd lhs, in VectorXd rhs)
	{
		if (lhs.Col != rhs.Size)
		{
			throw new IndexOutOfRangeException("Mismatch MatrixXd colume size!");
		}

		var result = new VectorXd(lhs.Row);

		for (var i = 0; i < lhs.Row; i++)
		{
			for (var j = 0; j < lhs.Col; j++)
			{
				result[i] += lhs[i, j] * rhs[j];
			}
		}

		return result;
	}

	public static MatrixXd operator *(in MatrixXd lhs, in MatrixXd rhs)
	{
		if (lhs.Col != rhs.Row)
		{
			throw new IndexOutOfRangeException("Mismatch MatrixXd lhs colume size and rhs row size!");
		}

		var result = new MatrixXd(lhs.Row, rhs.Col);

		for (var i = 0; i < lhs.Row; i++)
		{
			for (var j = 0; j < rhs.Col; j++)
			{
				for (var k = 0; k < lhs.Col; k++)
				{
					result[i, j] += lhs[i, k] * rhs[k, j];
				}
			}
		}

		return result;
	}

	public static implicit operator MatrixXd(in VectorXd v)
	{
		var result = new MatrixXd(v.Size, 1);

		for (var i = 0; i < v.Size; i++)
		{
			result[i, 0] = v[i];
		}

		return result;
	}
}
