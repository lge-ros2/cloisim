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

using System.Collections.Generic;

namespace Game.Utils.Triangulation
{
	/// <summary>
	/// Data tha describe a triangle and its context in a triangulation.
	/// </summary>
	public unsafe struct DelaunayTriangle
	{
		/// <summary>
		/// The indices of the points that define the triangle.
		/// </summary>
		public fixed int p[3];

		/// <summary>
		/// The indices of the triangles that are adjacent.
		/// </summary>
		public fixed int adjacent[3];

		private const int NO_ADJACENT_TRIANGLE = -1;

		/// <summary>
		/// Constructor that receives 3 vertex indices.
		/// </summary>
		/// <param name="point0">The index of the first vertex.</param>
		/// <param name="point1">The index of the second vertex.</param>
		/// <param name="point2">The index of the third vertex.</param>
		public DelaunayTriangle(int point0, int point1, int point2)
		{
			p[0] = point0;
			p[1] = point1;
			p[2] = point2;

			adjacent[0] = NO_ADJACENT_TRIANGLE;
			adjacent[1] = NO_ADJACENT_TRIANGLE;
			adjacent[2] = NO_ADJACENT_TRIANGLE;
		}

		/// <summary>
		/// Constructor that receives all the data.
		/// </summary>
		/// <param name="point0">The index of the first vertex.</param>
		/// <param name="point1">The index of the second vertex.</param>
		/// <param name="point2">The index of the third vertex.</param>
		/// <param name="adjacent0">The index of the triangle that is adjacent in the first edge.</param>
		/// <param name="adjacent1">The index of the triangle that is adjacent in the second edge.</param>
		/// <param name="adjacent2">The index of the triangle that is adjacent in the third edge.</param>
		public DelaunayTriangle(int point0, int point1, int point2, int adjacent0, int adjacent1, int adjacent2)
		{
			p[0] = point0;
			p[1] = point1;
			p[2] = point2;

			adjacent[0] = adjacent0;
			adjacent[1] = adjacent1;
			adjacent[2] = adjacent2;
		}

#if UNITY_EDITOR

		/// <summary>
		/// Gets the content of the triangle vertices array, which cannot be watch by Visual Studio unless you convert it to a list.
		/// </summary>
		public List<int> DebugP
		{
			get
			{
				List<int> debugArray = new List<int>(3);
				for (int i = 0; i < 3; ++i)
				{
					debugArray.Add(p[i]);
				}
				return debugArray;
			}
		}

		/// <summary>
		/// Gets the content of the triangle adjacents array, which cannot be watch by Visual Studio unless you convert it to a list.
		/// </summary>
		public List<int> DebugAdjacent
		{
			get
			{
				List<int> debugArray = new List<int>(3);
				for (int i = 0; i < 3; ++i)
				{
					debugArray.Add(adjacent[i]);
				}
				return debugArray;
			}
		}

#endif

	}
}

