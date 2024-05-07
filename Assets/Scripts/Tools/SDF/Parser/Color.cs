/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SDF
{
	public class Color
	{
		public double R = 0.0;
		public double G = 0.0;
		public double B = 0.0;
		public double A = 1.0;

		public void FromString(string value)
		{
			if (string.IsNullOrEmpty(value))
				return;

			value = value.Trim();

			var tmp = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			if (tmp.Length < 3)
				return;

			R = (double)Convert.ChangeType(tmp[0], TypeCode.Double);
			G = (double)Convert.ChangeType(tmp[1], TypeCode.Double);
			B = (double)Convert.ChangeType(tmp[2], TypeCode.Double);

			if (tmp.Length != 4)
				return;

			A = (double)Convert.ChangeType(tmp[3], TypeCode.Double);
		}

		public Color()
		: this(0.0, 0.0, 0.0, 1.0)
		{ }

		public Color(string value)
		{
			FromString(value);
		}

		public Color(double r, double g, double b, double a)
		{
			R = r;
			G = g;
			B = b;
			A = a;
		}
	}
}