/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TextureUtil
{
	public enum FillOptions
	{
		Overwrite,
		Lesser,
		Greater
	};

	private static Material _rotateMaterial = new Material(Shader.Find("Hidden/Rotate180"));
	private static readonly Dictionary<int, RenderTexture> _rtForSaveImageCache = new();

	static TextureUtil()
	{
#if UNITY_EDITOR
		AssemblyReloadEvents.beforeAssemblyReload += CleanUp;
		EditorApplication.quitting += CleanUp;
#endif
		Application.quitting += CleanUp;
	}

	private static void CleanUp()
	{
		foreach (var kv in _rtForSaveImageCache)
		{
			if (kv.Value != null)
			{
				kv.Value.Release();
				UnityEngine.Object.DestroyImmediate(kv.Value);
			}
		}
		_rtForSaveImageCache.Clear();

		if (_rotateMaterial != null)
		{
			UnityEngine.Object.DestroyImmediate(_rotateMaterial);
			_rotateMaterial = null;
		}
	}

	public static void Clear(this Texture2D texture)
	{
		Clear(texture, Color.clear);
	}

	public static void Clear(this Texture2D texture, in Color color)
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

	public static void FillCircle(
		this Texture2D texture, in float x, in float y, in float radius, in Color color,
		in FillOptions option = FillOptions.Overwrite)
	{
		FillCircle(texture, (int)x, (int)y, (int)radius, color, option);
	}

	public static void FillCircle(
		this Texture2D texture, in int x, in int y, in int radius, in Color color,
		in FillOptions option = FillOptions.Overwrite)
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
					var pixelColor = texture.GetPixel(u, v);
					switch (option)
					{
						case FillOptions.Lesser:
							pixelColor.r = (pixelColor.r < color.r) ? pixelColor.r : color.r;
							pixelColor.g = (pixelColor.g < color.g) ? pixelColor.g : color.g;
							pixelColor.b = (pixelColor.b < color.b) ? pixelColor.b : color.b;
							pixelColor.a = (pixelColor.a < color.a) ? pixelColor.a : color.a;
							break;

						case FillOptions.Greater:
							pixelColor.r = (pixelColor.r > color.r) ? pixelColor.r : color.r;
							pixelColor.g = (pixelColor.g > color.g) ? pixelColor.g : color.g;
							pixelColor.b = (pixelColor.b > color.b) ? pixelColor.b : color.b;
							pixelColor.a = (pixelColor.a > color.a) ? pixelColor.a : color.a;
							break;

						default:
						case FillOptions.Overwrite:
							pixelColor = color;
							break;
					}

					texture.SetPixel(u, v, pixelColor);
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

	private static void SaveRawImage(this Texture2D texture, in string path, in string name)
	{
		var id = texture.GetInstanceID();
		if (!_rtForSaveImageCache.TryGetValue(id, out var rt) ||
			rt == null || !rt.IsCreated() ||
			rt.width != texture.width || rt.height != texture.height)
		{
			if (rt != null)
				rt.Release();
			rt = new RenderTexture(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
			rt.Create();
			_rtForSaveImageCache[id] = rt;
		}

		Graphics.Blit(texture, rt, _rotateMaterial);

		RenderTexture.active = rt;
		texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
		texture.Apply();
		RenderTexture.active = null;

		var bytes = texture.EncodeToPNG();
		var fileName = string.Format("{0}/{1}.png", path, name);

		if (!Directory.Exists(path))
		{
			Directory.CreateDirectory(path);
		}
		System.IO.File.WriteAllBytes(fileName, bytes);

		UnityEngine.Object.DestroyImmediate(rt);
	}

	public static void SaveRawImage(this Texture2D texture, byte[] data, in string path, in string filename, in SensorDevices.CameraData.PixelFormat format)
	{
		if (format != SensorDevices.CameraData.PixelFormat.L_INT8)
		{
			Debug.LogWarning($"{format.ToString()} is not support to save file");
		}
		texture.SaveRawImage(data, path, filename);
	}

	public static void SaveRawImage(this Texture2D texture, byte[] data, in string path, in string filename)
	{
		texture.SetPixelData(data, 0);
		texture.Apply();
		texture.SaveRawImage(path, filename);
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