using UnityEngine;

namespace Game.Utils.Math
{
	public static class MathUtils
	{
		/// <summary>
		/// Calculates the determinan of a 3 columns x 3 rows matrix.
		/// </summary>
		/// <param name="m00">The element at position (0, 0).</param>
		/// <param name="m10">The element at position (1, 0).</param>
		/// <param name="m20">The element at position (2, 0).</param>
		/// <param name="m01">The element at position (0, 1).</param>
		/// <param name="m11">The element at position (1, 1).</param>
		/// <param name="m21">The element at position (2, 1).</param>
		/// <param name="m02">The element at position (0, 2).</param>
		/// <param name="m12">The element at position (1, 2).</param>
		/// <param name="m22">The element at position (2, 2).</param>
		/// <returns>The determinant.</returns>
		public static float CalculateMatrix3x3Determinant(float m00, float m10, float m20,
														  float m01, float m11, float m21,
														  float m02, float m12, float m22)
		{
			return m00 * m11 * m22 + m10 * m21 * m02 + m20 * m01 * m12 - m20 * m11 * m02 - m10 * m01 * m22 - m00 * m21 * m12;
		}

		/// <summary>
		/// Checks whether a point lays on the right side of an edge.
		/// </summary>
		/// <param name="edgeEndpointA">The first point of the edge.</param>
		/// <param name="edgeEndpointB">The second point of the edge.</param>
		/// <param name="point">The point to check.</param>
		/// <returns>True if the point is on the right side; False if the point is on the left side or is contained in the edge.</returns>
		public static bool IsPointToTheRightOfEdge(Vector2 edgeEndpointA, Vector2 edgeEndpointB, Vector2 point)
		{
			Vector2 aToB = (edgeEndpointB - edgeEndpointA).normalized; // Normalization is quite important to avoid precision loss!
			Vector2 aToP = (point - edgeEndpointA).normalized;
			Vector3 ab_x_p = Vector3.Cross(aToB, aToP);
			return ab_x_p.z < -0.0001f; // Note: Due to extremely small negative values were causing wrong results, a tolerance is used instead of zero
		}

		/// <summary>
		/// Checks whether a point is contained in a triangle. The vertices of the triangle must be sorted counter-clockwise.
		/// </summary>
		/// <param name="triangleP0">The first vertex of the triangle.</param>
		/// <param name="triangleP1">The second vertex of the triangle.</param>
		/// <param name="triangleP2">The third vertex of the triangle.</param>
		/// <param name="pointToCheck">The point that may be contained.</param>
		/// <returns>True if the point is contained in the triangle; false otherwise.</returns>
		public static bool IsPointInsideTriangle(Vector2 triangleP0, Vector2 triangleP1, Vector2 triangleP2, Vector2 pointToCheck)
		{
			Vector3 ab_x_p = Vector3.Cross(triangleP1 - triangleP0, pointToCheck);
			Vector3 bc_x_p = Vector3.Cross(triangleP2 - triangleP1, pointToCheck);
			Vector3 ca_x_p = Vector3.Cross(triangleP0 - triangleP2, pointToCheck);

			return ab_x_p.z == bc_x_p.z && ab_x_p.z == ca_x_p.z;
		}

		// https://gamedev.stackexchange.com/questions/71328/how-can-i-add-and-subtract-convex-polygons
		public static bool IsPointInsideCircumcircle(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 pointToCheck)
		{
			// This first part will simplify how we calculate the determinant
			float a = p0.x - pointToCheck.x;
			float d = p1.x - pointToCheck.x;
			float g = p2.x - pointToCheck.x;

			float b = p0.y - pointToCheck.y;
			float e = p1.y - pointToCheck.y;
			float h = p2.y - pointToCheck.y;

			float c = a * a + b * b;
			float f = d * d + e * e;
			float i = g * g + h * h;

			float determinant = (a * e * i) + (b * f * g) + (c * d * h) - (g * e * c) - (h * f * a) - (i * d * b);

			return determinant >= 0; // zero means on the perimeter
		}

		/// <summary>
		/// Calculates whether 2 line segments intersect and the intersection point.
		/// </summary>
		/// <param name="endpointA1">The first point of the first segment.</param>
		/// <param name="endpointB1">The second point of the first segment.</param>
		/// <param name="endpointA2">The first point of the second segment.</param>
		/// <param name="endpointB2">The second point of the second segment.</param>
		/// <param name="intersectionPoint">The intersection point, if any.</param>
		/// <returns>True if the line segment intersect; False otherwise.</returns>
		public static bool IntersectionBetweenLines(Vector2 endpointA1, Vector2 endpointB1, Vector2 endpointA2, Vector2 endpointB2, out Vector2 intersectionPoint)
		{
			// https://stackoverflow.com/questions/4543506/algorithm-for-intersection-of-2-lines

			intersectionPoint = new Vector2(float.MaxValue, float.MaxValue);

			bool isLine1Vertical = endpointB1.x == endpointA1.x;
			bool isLine2Vertical = endpointB2.x == endpointA2.x;

			float x = float.MaxValue;
			float y = float.MaxValue;

			if (isLine1Vertical && !isLine2Vertical)
			{
				// First it calculates the standard form (Ax + By = C)
				float m2 = (endpointB2.y - endpointA2.y) / (endpointB2.x - endpointA2.x);

				float A2 = m2;
				float C2 = endpointA2.x * m2 - endpointA2.y;

				x = endpointA1.x;
				y = m2 * endpointA1.x - C2;
			}
			else if (isLine2Vertical && !isLine1Vertical)
			{
				// First it calculates the standard form (Ax + By = C)
				float m1 = (endpointB1.y - endpointA1.y) / (endpointB1.x - endpointA1.x);

				float A1 = m1;
				float C1 = endpointA1.x * m1 - endpointA1.y;

				x = endpointA2.x;
				y = m1 * endpointA2.x - C1;
			}
			else if (!isLine1Vertical && !isLine2Vertical)
			{
				// First it calculates the standard form of both lines (Ax + By = C)
				float m1 = (endpointB1.y - endpointA1.y) / (endpointB1.x - endpointA1.x);

				float A1 = m1;
				float B1 = -1.0f;
				float C1 = endpointA1.x * m1 - endpointA1.y;

				float m2 = (endpointB2.y - endpointA2.y) / (endpointB2.x - endpointA2.x);

				float A2 = m2;
				float B2 = -1.0f;
				float C2 = endpointA2.x * m2 - endpointA2.y;

				float determinant = A1 * B2 - A2 * B1;

				if (determinant == 0)
				{
					// Lines do not intersect
					return false;
				}

				x = (B2 * C1 - B1 * C2) / determinant;
				y = (A1 * C2 - A2 * C1) / determinant;
			}
			// else : no intersection

			bool result = false;

			//Debug.DrawLine(endpointA1, new Vector2(x, y), Color.yellow, 10.0f);
			//Debug.DrawLine(endpointA2, new Vector2(x, y), Color.yellow, 10.0f);

			// Checks whether the point is in the segment determined by the endpoints of both lines
			if (x <= Mathf.Max(endpointA1.x, endpointB1.x) && x >= Mathf.Min(endpointA1.x, endpointB1.x) &&
				y <= Mathf.Max(endpointA1.y, endpointB1.y) && y >= Mathf.Min(endpointA1.y, endpointB1.y) &&
				x <= Mathf.Max(endpointA2.x, endpointB2.x) && x >= Mathf.Min(endpointA2.x, endpointB2.x) &&
				y <= Mathf.Max(endpointA2.y, endpointB2.y) && y >= Mathf.Min(endpointA2.y, endpointB2.y))
			{
				intersectionPoint.x = x;
				intersectionPoint.y = y;
				result = true;
			}

			return result;
		}

		public static bool IsTriangleVerticesCW(Vector2 point0, Vector2 point1, Vector2 point2)
		{
			return CalculateMatrix3x3Determinant(point0.x, point0.y, 1.0f,
												 point1.x, point1.y, 1.0f,
												 point2.x, point2.y, 1.0f) < 0.0f;
		}

		//Is a quadrilateral convex? Assume no 3 points are colinear and the shape doesnt look like an hourglass
		public static bool IsQuadrilateralConvex(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
		{
			bool isConvex = false;

			bool abc = IsTriangleVerticesCW(a, b, c);
			bool abd = IsTriangleVerticesCW(a, b, d);
			bool bcd = IsTriangleVerticesCW(b, c, d);
			bool cad = IsTriangleVerticesCW(c, a, d);

			if (abc && abd && bcd & !cad)
			{
				isConvex = true;
			}
			else if (abc && abd && !bcd & cad)
			{
				isConvex = true;
			}
			else if (abc && !abd && bcd & cad)
			{
				isConvex = true;
			}
			//The opposite sign, which makes everything inverted
			else if (!abc && !abd && !bcd & cad)
			{
				isConvex = true;
			}
			else if (!abc && !abd && bcd & !cad)
			{
				isConvex = true;
			}
			else if (!abc && abd && !bcd & !cad)
			{
				isConvex = true;
			}


			return isConvex;
		}

		/// <summary>
		/// Calcualtes the area of a triangle, according to its 3 vertices.
		/// </summary>
		/// <remarks>
		/// It does not matter whether the vertices are sorted counter-clockwise.
		/// </remarks>
		/// <param name="p0">The first vertex.</param>
		/// <param name="p1">The second vertex.</param>
		/// <param name="p2">The third vertex.</param>
		/// <returns>The area of the triangle.</returns>
		public static float CalculateTriangleArea(Vector2 p0, Vector2 p1, Vector2 p2)
		{
			return Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
		}
	}
}

