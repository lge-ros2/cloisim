/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace SDF
{
	public class Matrix3x3
	{
		private float3x3 _elements;

		public Matrix3x3(in float value = 0)
		{
			_elements = new float3x3(value);
		}

		public Matrix3x3(in float[,] elements)
		{
			if (9 == elements.Length)
			{
				_elements = new float3x3(
												elements[0, 0], elements[0, 1], elements[0, 2],
												elements[1, 0], elements[1, 1], elements[1, 2],
												elements[2, 0], elements[2, 1], elements[2, 2]);
			}
			else
			{
				Debug.LogError("Wrong size of matrix");
			}
		}

		public Matrix3x3(in float m00, in float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
		{
			_elements = new float3x3(m00, m01, m02, m10, m11, m12, m20, m21, m22);
		}

		public Matrix3x3(in float3x3 matrix)
		{
			_elements = matrix;
		}

		public float this[int i, int j]
		{
			get { return _elements[i][j]; }
			set { _elements[i][j] = value; }
		}

		public static Matrix3x3 operator +(in Matrix3x3 A, in Matrix3x3 B)
		{
			return new Matrix3x3(A._elements + B._elements);
		}

		public static Matrix3x3 operator +(in Matrix3x3 A, in float x)
		{
			return new Matrix3x3(A._elements + x);
		}

		public static Matrix3x3 operator -(in Matrix3x3 A, in Matrix3x3 B)
		{
			return new Matrix3x3(A._elements - B._elements);
		}

		public static Matrix3x3 operator -(in Matrix3x3 A, in float x)
		{
			return new Matrix3x3(A._elements - x);
		}

		public static Matrix3x3 operator *(in Matrix3x3 A, in float x)
		{
			return new Matrix3x3(A._elements * x);
		}

		public static Matrix3x3 operator *(in Matrix3x3 A, in Matrix3x3 B)
		{
			return new Matrix3x3(A._elements * B._elements);
		}

		public static Vector3 operator *(in Matrix3x3 A, in Vector3 B)
		{
			var row0 = Vector3.Dot(new Vector3(A[0, 0], A[0, 1], A[0, 2]), B);
			var row1 = Vector3.Dot(new Vector3(A[1, 0], A[1, 1], A[1, 2]), B);
			var row2 = Vector3.Dot(new Vector3(A[2, 0], A[2, 1], A[2, 2]), B);
			return new Vector3(row0, row1, row2);
		}

		public float Determinant()
		{
			var result = 0.0f;
			for (int i = 0; i < 3; i++)
			{
				result += (this[0, i] * (this[1, (i + 1) % 3] * this[2, (i + 2) % 3] - this[1, (i + 2) % 3] * this[2, (i + 1) % 3]));
			}
			return result;
		}

		public float Trace()
		{
			return (this[0, 0] + this[1, 1] + this[2, 2]);
		}

		public bool IsDiagonal()
		{
			for (int i = 0; i < 3; i++)
				for (int j = 0; j < 3; j++)
					if (i != j && this[i, j] != 0)
						return false;
			return true;
		}

		public Matrix3x3 Transpose()
		{
			return new Matrix3x3(this[0, 0], this[1, 0], this[2, 0],
													 this[0, 1], this[1, 1], this[2, 1],
													 this[0, 2], this[1, 2], this[2, 2]);
		}

		public void DiagonalizeRealSymmetric(out Vector3 eigenValues, out Vector3[] eigenVectors)
		{
			if (IsDiagonal())
			{
				eigenValues = new Vector3(_elements[0][0], _elements[1][1], _elements[2][2]);
				eigenVectors = new Vector3[] { new Vector3(1, 0, 0),
																					new Vector3(0, 1, 0),
																					new Vector3(0, 0, 1) };
				return;
			}

			eigenValues = EigenValues();
			eigenVectors = EigenVectors(new float[] { eigenValues[0], eigenValues[1], eigenValues[2] });

		}

		public Vector3 EigenValues()
		{
			/* Smith, Oliver K. (April 1961), "Eigenvalues of a symmetric 3 × 3 matrix."
			 * Communications of the ACM, 4 (4): 168, doi:10.1145/355578.366316             */

			var matrix = this;

			var traceA = matrix.Trace();
			var q = traceA / 3;
			var p1 = matrix[0, 1] * matrix[0, 1] + matrix[0, 2] * matrix[0, 2] + matrix[1, 2] * matrix[1, 2];
			var p2 = (matrix[0, 0] - q) * (matrix[0, 0] - q) + (matrix[1, 1] - q) * (matrix[1, 1] - q) + (matrix[2, 2] - q) * (matrix[2, 2] - q) + 2 * p1;
			var p = Mathf.Sqrt(p2 / 6);

			var B = (matrix * (1f / p)) + (new Matrix3x3(new float[,] { { -q / p, 0, 0 }, { 0, -q / p, 0 }, { 0, 0, -q / p } }));
			var angle = Mathf.Clamp(B.Determinant() / 2f, -1, 1);
			var theta = Mathf.Acos(angle) / 3;

			var beta = new Vector3();
			var alpha = new Vector3();
			for (int k = 0; k < 3; k++)
			{
				beta[k] = 2f * Mathf.Cos(theta + (2 * Mathf.PI * k) / 3);
				alpha[k] = p * beta[k] + q;
			}

			return alpha;
		}

		private Vector3[] EigenVectors(float[] eigenvalues_unsorted)
		{
			var eigenvalues = (float[])eigenvalues_unsorted.Clone();
			System.Array.Sort(eigenvalues);

			var eigenvector0 = GetEigenvector0(eigenvalues);
			var eigenvector1 = GetEigenvector1(eigenvalues, eigenvector0);
			var eigenvector2 = GetEigenvector2(eigenvalues, eigenvector0, eigenvector1);

			var values = new List<float> { eigenvalues[0], eigenvalues[1], eigenvalues[2] };
			var vectors = new List<float[]>(){
										new float[] { eigenvector0[0], eigenvector0[1], eigenvector0[2] },
										new float[] { eigenvector1[0], eigenvector1[1], eigenvector1[2] },
										new float[] { eigenvector2[0], eigenvector2[1], eigenvector2[2] }
										};
			var values_unsorted = new List<float>();
			var vectors_unsorted = new List<float[]>();

			for (int i = 0; i < 3; i++)
			{
				int idx = values.IndexOf(eigenvalues_unsorted[i]);
				values_unsorted.Add(values[idx]);
				vectors_unsorted.Add(vectors[idx]);
				values.RemoveAt(idx);
				vectors.RemoveAt(idx);
			}

			return new Vector3[] {new Vector3(vectors_unsorted[0][0], vectors_unsorted[0][1], vectors_unsorted[0][2]),
														new Vector3(vectors_unsorted[1][0], vectors_unsorted[1][1], vectors_unsorted[1][2]),
														new Vector3(vectors_unsorted[2][0], vectors_unsorted[2][1], vectors_unsorted[2][2]) };
		}

		private Vector3 GetEigenvector0(float[] eigenvalues)
		{
			if (IsTwoEigenvaluesEqual(eigenvalues))
				return new Vector3(1, 0, 0);

			var row0 = new Vector3(_elements[0][0] - eigenvalues[0], _elements[0][1], _elements[0][2]);
			var row1 = new Vector3(_elements[1][0], _elements[1][1] - eigenvalues[0], _elements[1][2]);
			var row2 = new Vector3(_elements[2][0], _elements[2][1], _elements[2][2] - eigenvalues[0]);

			var cross_r0r1 = Vector3.Cross(row0, row1);
			var cross_r0r2 = Vector3.Cross(row0, row2);
			var cross_r1r2 = Vector3.Cross(row1, row2);

			var dot0 = Vector3.Dot(cross_r0r1, cross_r0r1);
			var dot1 = Vector3.Dot(cross_r0r2, cross_r0r2);
			var dot2 = Vector3.Dot(cross_r1r2, cross_r1r2);
			var dmax = dot0;
			int imax = 0;

			if (dot1 > dmax) { dmax = dot1; imax = 1; }
			if (dot2 > dmax) { imax = 2; }

			Vector3 eigenvector0;
			if (imax == 0)
				eigenvector0 = new Vector3(cross_r0r1[0] / Mathf.Sqrt(dot0), cross_r0r1[1] / Mathf.Sqrt(dot0), cross_r0r1[2] / Mathf.Sqrt(dot0));
			else if (imax == 1)
				eigenvector0 = new Vector3(cross_r0r2[0] / Mathf.Sqrt(dot1), cross_r0r2[1] / Mathf.Sqrt(dot1), cross_r0r2[2] / Mathf.Sqrt(dot1));
			else
				eigenvector0 = new Vector3(cross_r1r2[0] / Mathf.Sqrt(dot2), cross_r1r2[1] / Mathf.Sqrt(dot2), cross_r1r2[2] / Mathf.Sqrt(dot2));

			return eigenvector0;
		}

		private Vector3 GetEigenvector1(float[] eigenvalues, Vector3 eigenvector0)
		{
			Matrix3x3 inertiaTensor = this;

			if (IsTwoEigenvaluesEqual(eigenvalues))
				return new Vector3(0, 1, 0);

			var eigenvector1 = Vector3.zero;
			var UV = CalculateOrthogonalComplement(eigenvector0);
			var AU = inertiaTensor * UV[0];
			var AV = inertiaTensor * UV[1];

			var m00 = Vector3.Dot(UV[0], AU) - eigenvalues[1];
			var m01 = Vector3.Dot(UV[0], AV);
			var m11 = Vector3.Dot(UV[1], AV) - eigenvalues[1];

			if (Mathf.Abs(m00) > Mathf.Abs(m11))
			{
				var maxAbscomp = Mathf.Max(Mathf.Abs(m00), Mathf.Abs(m01));
				if (maxAbscomp > 0)
				{
					if (Mathf.Abs(m00) >= Mathf.Abs(m01))
					{
						m01 /= m00;
						m00 = 1 / Mathf.Sqrt(1 + m01 * m01);
						m01 *= m00;
					}
					else
					{
						m00 /= m01;
						m01 = 1 / Mathf.Sqrt(1 + m00 * m00);
						m00 *= m01;
					}
					eigenvector1 = (UV[0] * m01) + (UV[1] * -m00);
				}
				else
					eigenvector1 = UV[0];
			}

			else
			{
				var maxAbscomp = Mathf.Max(Mathf.Abs(m11), Mathf.Abs(m01));
				if (maxAbscomp > 0)
				{
					if (Mathf.Abs(m11) >= Mathf.Abs(m01))
					{
						m01 /= m11;
						m11 = 1 / Mathf.Sqrt(1 + m01 * m01);
						m01 *= m11;
					}
					else
					{
						m11 /= m01;
						m01 = 1 / Mathf.Sqrt(1 + m11 * m11);
						m11 *= m01;
					}
					eigenvector1 = (UV[0] * m11) + (UV[1] * -m00);
				}
				else
					eigenvector1 = UV[0];
			}

			return eigenvector1;
		}

		private Vector3 GetEigenvector2(float[] eigenvalues, Vector3 eigenvector0, Vector3 eigenvector1)
		{
			return (IsTwoEigenvaluesEqual(eigenvalues)) ? new Vector3(0, 0, 1) : Vector3.Cross(eigenvector0, eigenvector1);
		}

		private Vector3[] CalculateOrthogonalComplement(Vector3 W)
		{
			float invLength = 0;
			Vector3 U;

			if (Mathf.Abs(W[0]) > Mathf.Abs(W[1]))
			{
				invLength = 1 / Mathf.Sqrt(W[0] * W[0] + W[2] * W[2]);
				U = new Vector3(-W[2] * invLength, 0, W[0] * invLength);
			}
			else
			{
				invLength = 1 / Mathf.Sqrt(W[1] * W[1] + W[2] * W[2]);
				U = new Vector3(0, W[2] * invLength, -W[1] * invLength);
			}

			var V = Vector3.Cross(W, U);

			return new Vector3[] { U, V };
		}

		private bool IsTwoEigenvaluesEqual(float[] eigenvalues)
		{
			return (eigenvalues[0] == eigenvalues[1] || eigenvalues[1] == eigenvalues[2] || eigenvalues[0] == eigenvalues[2]);
		}
	}
}