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
using UnityEngine;

namespace Game.Utils.Triangulation
{
	/// <summary>
	/// A data structure that sorts a list of points by their proximity. It is a grid that divides the 2D space in NxN cells,
	/// each of them acting as a "bin" that contains points.
	/// </summary>
	public class PointBinGrid
	{
		/// <summary>
		/// The cells of the grid. Each of them may or may not store a bin with points.
		/// </summary>
		public List<Vector2>[] Cells;

		// The size of a cell in 2D space.
		private Vector2 m_cellSize;

		// The size of the grid in the 2D space.
		private Vector2 m_gridSize; // Xmax, Ymax

		// The amount of cells per side.
		private int m_cellsPerSide; // n

		/// <summary>
		/// Constructore that receives the amount of cells per side of the grid, and the size of the grid in the 2D space.
		/// </summary>
		/// <param name="cellsPerSide">The amount of cells per side of the grid.</param>
		/// <param name="gridSize">The size of the grid in the 2D space.</param>
		public PointBinGrid(int cellsPerSide, Vector2 gridSize)
		{
			Cells = new List<Vector2>[cellsPerSide * cellsPerSide];
			m_cellSize = gridSize / cellsPerSide;
			m_gridSize = gridSize;
			m_cellsPerSide = cellsPerSide;
		}

		/// <summary>
		/// Adds a point to a bin of the grid, according to its position in 2D space.
		/// </summary>
		/// <param name="newPoint">The point to add.</param>
		public void AddPoint(Vector2 newPoint)
		{
			int rowIndex = (int)(0.99f * m_cellsPerSide * newPoint.y / m_gridSize.y); // i
			int columnIndex = (int)(0.99f * m_cellsPerSide * newPoint.x / m_gridSize.x); // j

			int binIndex = 0; // b

			if (rowIndex % 2 == 0)
			{
				binIndex = rowIndex * m_cellsPerSide + columnIndex + 1;
			}
			else
			{
				binIndex = (rowIndex + 1) * m_cellsPerSide - columnIndex;
			}

			binIndex--; // zero-based index

			if (Cells[binIndex] == null)
			{
				Cells[binIndex] = new List<Vector2>();
			}

			Cells[binIndex].Add(newPoint);

			//DrawPointAddition(newPoint, columnIndex, rowIndex);
		}

		public void DrawGrid(Color color, float duration)
		{
			for (int i = 0; i < m_cellsPerSide; ++i)
			{
				Debug.DrawRay(new Vector3(0.0f, i * m_cellSize.y, 1.0f), Vector2.right * m_gridSize.x, color, duration);

				for (int j = 0; j < m_cellsPerSide; ++j)
				{
					Debug.DrawRay(new Vector3(j * m_cellSize.x, 0.0f, 1.0f), Vector2.up * m_gridSize.y, color, duration);
				}
			}

			Debug.DrawRay(new Vector3(0.0f, m_cellsPerSide * m_cellSize.y, 1.0f), Vector2.right * m_gridSize.x, color, duration);
			Debug.DrawRay(new Vector3(m_cellsPerSide * m_cellSize.x, 0.0f, 1.0f), Vector2.up * m_gridSize.y, color, duration);
		}

		protected void DrawPointAddition(Vector2 point, int columnIndex, int rowIndex)
		{
			Vector2 cellBottomLeftCorner = new Vector2(columnIndex * m_cellSize.x, rowIndex * m_cellSize.y);
			Debug.DrawLine(point, cellBottomLeftCorner + m_cellSize * 0.5f, Color.cyan, 5.0f);
		}
	}
}

