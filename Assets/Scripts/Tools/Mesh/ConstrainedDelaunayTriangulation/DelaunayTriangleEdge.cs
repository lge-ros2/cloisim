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

namespace Game.Utils.Triangulation
{
	/// <summary>
	/// Data that describes the edge of a triangle.
	/// </summary>
	public struct DelaunayTriangleEdge
	{
		/// <summary>
		/// The index of the triangle.
		/// </summary>
		public int TriangleIndex;

		/// <summary>
		/// The local index of the edge in the triangle (0, 1 or 2).
		/// </summary>
		public int EdgeIndex;

		/// <summary>
		/// The index of the first vertex that form the edge.
		/// </summary>
		public int EdgeVertexA;

		/// <summary>
		/// The index of the second vertex that form the edge.
		/// </summary>
		public int EdgeVertexB;

		/// <summary>
		/// Constructor that receives all the data.
		/// </summary>
		/// <param name="triangleIndex">The index of the triangle.</param>
		/// <param name="edgeIndex">The local index of the edge (0, 1 or 2).</param>
		/// <param name="edgeVertexA">The index of the first vertex that form the edge.</param>
		/// <param name="edgeVertexB">The index of the second vertex that form the edge.</param>
		public DelaunayTriangleEdge(int triangleIndex, int edgeIndex, int edgeVertexA, int edgeVertexB)
		{
			TriangleIndex = triangleIndex;
			EdgeIndex = edgeIndex;
			EdgeVertexA = edgeVertexA;
			EdgeVertexB = edgeVertexB;
		}
	}
}

