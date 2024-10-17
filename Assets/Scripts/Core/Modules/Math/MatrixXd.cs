/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public struct MatrixXd
{
	private double[,] _elements;
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
		var str = "MatrixXd \n";
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

			if (m != n)
			{
				throw new InvalidOperationException("Matrix must be square to compute its inverse.");
			}

			var augmentedMatrix = new double[m, 2 * n];

			// Copy the original matrix and append the identity matrix
			for (var i = 0; i < m; i++)
			{
				for (var j = 0; j < n; j++)
				{
					augmentedMatrix[i, j] = this[i, j];
				}
				for (var j = 0; j < n; j++)
				{
					augmentedMatrix[i, j + n] = (i == j) ? 1.0 : 0.0;
				}
			}

			// Perform Gaussian elimination
			for (var k = 0; k < m; k++)
			{
				if (augmentedMatrix[k, k] == 0)
				{
					// Find a row to swap
					var swapRow = -1;
					for (var i = k + 1; i < m; i++)
					{
						if (augmentedMatrix[i, k] != 0)
						{
							swapRow = i;
							break;
						}
					}
					if (swapRow == -1)
					{
						throw new InvalidOperationException("Matrix is singular and cannot be inverted.");
					}

					// Swap rows
					for (var j = 0; j < 2 * n; j++)
					{
						var temp = augmentedMatrix[k, j];
						augmentedMatrix[k, j] = augmentedMatrix[swapRow, j];
						augmentedMatrix[swapRow, j] = temp;
					}
				}

				// Normalize the pivot row
				var pivot = augmentedMatrix[k, k];
				for (var j = 0; j < 2 * n; j++)
				{
					augmentedMatrix[k, j] /= pivot;
				}

				// Eliminate the current column in other rows
				for (var i = 0; i < m; i++)
				{
					if (i != k)
					{
						var factor = augmentedMatrix[i, k];
						for (var j = 0; j < 2 * n; j++)
						{
							augmentedMatrix[i, j] -= factor * augmentedMatrix[k, j];
						}
					}
				}
			}

			// Extract the inverse matrix
			var result = new MatrixXd(m, n);
			for (var i = 0; i < m; i++)
			{
				for (var j = 0; j < n; j++)
				{
					result[i, j] = augmentedMatrix[i, j + n];
				}
			}

			return result;
		}
	}

	public static bool operator ==(in MatrixXd lhs, in MatrixXd rhs)
	{
		if (lhs.Row != rhs.Row || lhs.Col != rhs.Col)
		{
			return false;
		}

		for (var i = 0; i < lhs.Row; i++)
		{
			for (var j = 0; j < lhs.Col; j++)
			{
				if (lhs[i, j] != rhs[i, j])
				{
					return false;
				}
			}
		}

		return true;
	}

	public static bool operator !=(in MatrixXd lhs, in MatrixXd rhs)
	{
		return !(lhs == rhs);
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
				result[i, j] *= rhs;
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
				for (var k = 0; k < rhs.Row; k++)
				{
					result[i, j] += lhs[i, k] * rhs[k, j];
				}
			}
		}

		return result;
	}
}
