/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System;

public struct VectorXd
{
	private double[] _elements;
	public int Size => _elements.Length;

	public VectorXd(in int capacity)
	{
		_elements = new double[capacity];
	}

	public VectorXd(IReadOnlyCollection<double> values)
		: this(values.Count())
	{
		_elements = values.ToArray();
	}

	public VectorXd(in VectorXd vec)
		: this(vec.Size)
	{
		for (var i = 0; i < Size; i++)
		{
			this[i] = vec[i];
		}
	}

	public double this[int index]
	{
		get
		{
			LengthCheck(index);
			return _elements[index];
		}
		set
		{
			LengthCheck(index);
			_elements[index] = value;
		}
	}

	private void LengthCheck(in int index)
	{
		if (index < 0 || index >= Size)
		{
			throw new IndexOutOfRangeException("Index is out of range.");
		}
	}

	private static void CapacityCheck(in VectorXd a, in VectorXd b)
	{
		if (a.Size != b.Size)
		{
			throw new IndexOutOfRangeException("Mismatch VectorXd Capacity!");
		}
	}

	private static void CapacityCheck(in VectorXd a, in int capacity)
	{
		if (a.Size != capacity)
		{
			throw new IndexOutOfRangeException("Mismatch VectorXd Capacity!");
		}
	}

	private static void ZeroCheck(in double value)
	{
		if (double.IsNaN(value) || Math.Abs(value) < Double.Epsilon)
		{
			throw new DivideByZeroException("Cannot divide by Zero!!");
		}
	}

	public static VectorXd Zero(in int capacity)
	{
		var vec = new VectorXd(capacity);
		for (var i = 0; i < vec.Size; i++)
		{
			vec[i] = 0;
		}
		return vec;
	}

	public override int GetHashCode()
	{
		var hashCode = 0;
		for (var i = 0; i < this.Size; i++)
		{
			hashCode ^= this[i].GetHashCode();
		}
		return hashCode;
	}

	public override bool Equals(object other)
	{
		return this == (VectorXd)other;
	}

	public override string ToString()
	{
		return ToString("G");
	}

	public string ToString(in string format)
	{
		var str = "VectorXd (";
		for (var i = 0; i < this.Size; i++)
		{
			str += this[i].ToString(format) + (i == this.Size - 1 ? "" : ", ");
		}
		str += ")";
		return str;
	}

	public static VectorXd operator +(in VectorXd a, in VectorXd b)
	{
		CapacityCheck(a, b);

		var c = new VectorXd(a);

		for (var i = 0; i < c.Size; i++)
		{
			c[i] += b[i];
		}

		return c;
	}

	public static VectorXd operator -(in VectorXd a)
	{
		var b = new VectorXd(a);

		for (var i = 0; i < b.Size; i++)
		{
			b[i] = -b[i];
		}

		return b;
	}

	public static VectorXd operator -(in VectorXd a, in VectorXd b)
	{
		CapacityCheck(a, b);

		var c = new VectorXd(a);

		for (var i = 0; i < c.Size; i++)
		{
			c[i] -= b[i];
		}

		return c;
	}

	public static VectorXd operator *(in double d, in VectorXd a)
	{
		var c = new VectorXd(a);

		for (var i = 0; i < c.Size; i++)
		{
			c[i] *= d;
		}

		return c;
	}


	public static VectorXd operator *(in VectorXd a, in double d)
	{
		return d * a;
	}

	public static VectorXd operator *(in VectorXd a, in VectorXd b)
	{
		CapacityCheck(a, b);

		var c = new VectorXd(a);

		for (var i = 0; i < c.Size; i++)
		{
			c[i] *= b[i];
		}

		return c;
	}

	public static VectorXd operator /(in VectorXd a, in double d)
	{
		ZeroCheck(d);

		var c = new VectorXd(a);

		for (var i = 0; i < c.Size; i++)
		{
			c[i] /= d;
		}

		return c;
	}

	public static bool operator ==(in VectorXd lhs, in VectorXd rhs)
	{
		for (var i = 0; i < lhs.Size; i++)
		{
			if (Math.Abs(lhs[i] - rhs[i]) > Double.Epsilon)
			{
				return false;
			}
		}

		return true;
	}

	public static bool operator !=(in VectorXd lhs, in VectorXd rhs)
	{
		return !(lhs == rhs);
	}
}
