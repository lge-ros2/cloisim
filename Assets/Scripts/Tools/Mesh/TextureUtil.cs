/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.IO;
using UnityEngine;
using Unity.Collections;

public static class TextureUtil
{
	public static void Clear(this Texture2D texture)
	{
		Clear(texture, Color.clear);
	}

	public static void Clear(this Texture2D texture, Color color)
	{
		Fill(texture, color);
	}

	public static void Fill(this Texture2D texture, in Color color)
	{
#if false
		for (var i = 0; i < texture.width; i++)
		{
			for (var j = 0; j < texture.height; j++)
			{
				texture.SetPixel(i, j, color);
			}
		}
#else
		var pixels = new Color[texture.width * texture.height];
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = color;
		}

		Fill(texture, ref pixels);
#endif
	}

	public static void Fill(this Texture2D texture, ref Color[] colors)
	{
		texture.SetPixels(colors);
	}

	public static void FillCircle(this Texture2D texture, in float x, in float y, in float radius, in Color color)
	{
		FillCircle(texture, (int)x, (int)y, (int)radius, color);
	}

	public static void FillCircle(this Texture2D texture, in int x, in int y, in int radius, in Color color)
	{
		var rSquared = radius * radius;

		var minX = System.Math.Clamp(x - radius, 0, texture.width);
		var minY = System.Math.Clamp(y - radius, 0, texture.height);
		var maxX = System.Math.Clamp(x + radius + 1, 0, texture.width);
		var maxY = System.Math.Clamp(y + radius + 1, 0, texture.height);

		for (var u = minX; u < maxX; u++)
		{
			for (var v = minY; v < maxY; v++)
			{
				if ((x - u) * (x - u) + (y - v) * (y - v) < rSquared)
				{
					texture.SetPixel(u, v, color);
				}
			}
		}

		texture.Apply();
	}

	public static void FillTriangle(this Texture2D texture, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
	{
		var minX = Mathf.FloorToInt(Mathf.Min(v1.x, v2.x, v3.x));
		var minY = Mathf.FloorToInt(Mathf.Min(v1.y, v2.y, v3.y));
		var maxX = Mathf.CeilToInt(Mathf.Max(v1.x, v2.x, v3.x));
		var maxY = Mathf.CeilToInt(Mathf.Max(v1.y, v2.y, v3.y));

		minX = System.Math.Clamp(minX, 0, texture.width);
		minY = System.Math.Clamp(minY, 0, texture.height);
		maxX = System.Math.Clamp(maxX, 0, texture.width);
		maxY = System.Math.Clamp(maxY, 0, texture.height);

		// Iterate over the pixels within the bounding box and set the color of the pixels inside the triangle:
		for (var x = minX; x < maxX; x++)
		{
			for (var y = minY; y < maxY; y++)
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

	public static void SaveRawImage(this Texture2D texture, in NativeArray<byte> buffer, in string path, in string name)
	{
		texture.SetPixelData(buffer, 0);
		texture.Apply();
		var bytes = texture.EncodeToPNG();
		var fileName = string.Format("{0}/{1}.png", path, name);
		System.IO.File.WriteAllBytes(fileName, bytes);
	}

	public static void SaveRawImage(this Texture2D texture, byte[] data, in string path, in string name)
	{
		texture.SetPixelData(data, 0);
		texture.Apply();
		var bytes = texture.EncodeToPNG();
		var fileName = string.Format("{0}/{1}.png", path, name);
		System.IO.File.WriteAllBytes(fileName, bytes);
	}

	public static Texture2D LoadTGA(in string fileName)
	{
		using (var imageFile = File.OpenRead(fileName))
		{
			return LoadTGA(imageFile);
		}
	}

	public static Texture2D LoadTGA(in Stream stream)
	{
		if (stream.Length == 0)
		{
			return null;
		}

		using (var r = new BinaryReader(stream))
		{
			// Skip some header info we don't care about.
			// Even if we did care, we have to move the stream seek point to the beginning,
			// as the previous method in the workflow left it at the end.
			r.BaseStream.Seek(12, SeekOrigin.Begin);

			var width = r.ReadInt16();
			var height = r.ReadInt16();
			var bitDepth = r.ReadByte();

			// Skip a byte of header information we don't care about.
			r.BaseStream.Seek(1, SeekOrigin.Current);

			var texture = new Texture2D(width, height);
			var pulledColors = new Color32[width * height];

			if (bitDepth == 32)
			{
				for (var i = 0; i < width * height; i++)
				{
					var red = r.ReadByte();
					var green = r.ReadByte();
					var blue = r.ReadByte();
					var alpha = r.ReadByte();

					pulledColors[i] = new Color32(blue, green, red, alpha);
				}
			}
			else if (bitDepth == 24)
			{
				for (var i = 0; i < width * height; i++)
				{
					var red = r.ReadByte();
					var green = r.ReadByte();
					var blue = r.ReadByte();

					pulledColors[i] = new Color32(blue, green, red, 1);
				}
			}
			else
			{
				throw new Exception("TGA texture had non 32/24 bit depth.");
			}

			texture.SetPixels32(pulledColors);
			texture.Apply();

			return texture;
		}
	}
}