// Copyright 2021 Alejandro Villalba Avila
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.

using UnityEngine;

namespace Game.Utils.Math
{
	/// <summary>
	/// A 2D triangle.
	/// </summary>
	public struct Triangle2D
	{
		/// <summary>
		/// The first vertex.
		/// </summary>
		public Vector2 p0;

		/// <summary>
		/// The second vertex.
		/// </summary>
		public Vector2 p1;

		/// <summary>
		/// The third vertex.
		/// </summary>
		public Vector2 p2;

		/// <summary>
		/// Constructor that receives the 3 vertices.
		/// </summary>
		/// <param name="point0">The first vertex.</param>
		/// <param name="point1">The second vertex.</param>
		/// <param name="point2">The third vertex.</param>
		public Triangle2D(Vector2 point0, Vector2 point1, Vector2 point2)
		{
			p0 = point0;
			p1 = point1;
			p2 = point2;
		}

		/// <summary>
		/// Gets a vertex by its index.
		/// </summary>
		/// <param name="index">The index of the vertex, from 0 to 2.</param>
		/// <returns>The vertex.</returns>
		public Vector2 this[int index]
		{
			get
			{
				Debug.Assert(index >= 0 && index < 4, "The index of the triangle vertex must be in the range [0, 2].");

				return index == 0 ? p0 : index == 1 ? p1 : p2;
			}
		}

	}
}

