/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public static class TextureUtil
{
	public static Texture2D FillCircle(this Texture2D texture, int x, int y, int radius, Color color)
	{
		var rSquared = radius * radius;

		for (var u = x - radius; u < x + radius + 1; u++)
			for (var v = y - radius; v < y + radius + 1; v++)
				if ((x - u) * (x - u) + (y - v) * (y - v) < rSquared)
					texture.SetPixel(u, v, color);

		texture.Apply();

		return texture;
	}

	public static void FillTriangle(this Texture2D texture, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
	{
		var minX = Mathf.FloorToInt(Mathf.Min(v1.x, v2.x, v3.x));
		var minY = Mathf.FloorToInt(Mathf.Min(v1.y, v2.y, v3.y));
		var maxX = Mathf.CeilToInt(Mathf.Max(v1.x, v2.x, v3.x));
		var maxY = Mathf.CeilToInt(Mathf.Max(v1.y, v2.y, v3.y));

		// Iterate over the pixels within the bounding box and set the color of the pixels inside the triangle:
		for (var x = minX; x <= maxX; x++)
		{
			for (var y = minY; y <= maxY; y++)
			{
				var pixelCoord = new Vector2(x, y);
				if (IsPointInTriangle(pixelCoord, v1, v2, v3))
				{
					texture.SetPixel(x, y, color);
				}
			}
		}

		texture.Apply();
	}

	private static bool IsPointInTriangle(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3)
	{
		// Calculate the barycentric coordinates
		var dominator = (v2.y - v3.y) * (v1.x - v3.x) + (v3.x - v2.x) * (v1.y - v3.y);
		if (Mathf.Abs(dominator) < float.Epsilon)
		{
			// Triangle is degenerate (i.e., all vertices are collinear)
			// Debug.LogWarning("denominator == 0 ");
			return false;
		}

		var alpha = ((v2.y - v3.y) * (p.x - v3.x) + (v3.x - v2.x) * (p.y - v3.y)) /
					  dominator;
		var beta = ((v3.y - v1.y) * (p.x - v3.x) + (v1.x - v3.x) * (p.y - v3.y)) /
					  dominator;
		var gamma = 1 - alpha - beta;

		// Check if the point is inside the triangle
		return alpha >= 0 && beta >= 0 && gamma >= 0;
	}
}