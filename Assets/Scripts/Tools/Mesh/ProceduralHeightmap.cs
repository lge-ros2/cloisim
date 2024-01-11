/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System;

public class ProceduralHeightmap
{
	private static readonly string terrainShaderName = "Universal Render Pipeline/Terrain/Lit";
	private static readonly Shader TerrainShader = Shader.Find(terrainShaderName);

	private static readonly int ResolutionPerPatch = 32;
	private static readonly int MinDetailResolution = 8;
	private static readonly float DefaultSmootheness = 0.3f;

	private static void UpdateHeightMap(TerrainData terrainData, in Texture2D heightmapTexture)
	{
		// TerrainData terrainData = this.GetComponent<TerrainCollider>().terrainData;
		var terrainWidth = terrainData.heightmapResolution;
		var terrainHeight = terrainData.heightmapResolution;

		// Debug.Log("Terrain width,height=" + terrainWidth + ", " + terrainHeight);
		var heightValues = terrainData.GetHeights(0, 0, terrainWidth, terrainHeight);

		for (var terrainY = 0; terrainY < terrainHeight; terrainY++)
		{
			if (terrainY >= heightmapTexture.height)
				break;

			for (var terrainX = 0; terrainX < terrainWidth; terrainX++)
			{
				if (terrainX >= heightmapTexture.width)
					break;

				var heightColor = heightmapTexture.GetPixel(terrainY, terrainX);
				heightValues[terrainX, terrainY] = heightColor.grayscale;
				// Debug.Log(heightValues[terrainX, terrainY]);
			}
		}
		terrainData.SetHeights(0, 0, heightValues);
	}

	private static Texture2D GenerateTexture(in string uri)
	{
		// Debug.Log(uri);
		var img = System.Drawing.Image.FromFile(uri);
		var texture = new Texture2D(1, 1);

		// TODO: Support GeoTIFF
		using (var ms = new MemoryStream())
		{
			img.Save(ms, img.RawFormat);
			ImageConversion.LoadImage(texture, ms.ToArray(), false);
		}

		return texture;
	}

	private static byte[] GetBytesFromImage(in string imagePath)
	{
		try
		{
			var img = new Bitmap(imagePath);
			using (var ms = new MemoryStream())
			{
				img.Save(ms, img.RawFormat);
				return ms.ToArray();
			}
		}
		catch (Exception e)
		{
			Debug.LogError("Failed to load image: " + e.Message);
		}

		return null;
	}

	public static void Generate(in SDF.Heightmap property, in GameObject heightmapObject, in bool isVisualMesh)
	{
		var byteArray = GetBytesFromImage(property.uri);
		if (byteArray == null)
		{
			return;
		}

		TerrainData terrainData = null;
		var parentObject = heightmapObject.transform.parent;

		if (isVisualMesh)
		{
			var terrainCollider = parentObject.parent.GetComponentInChildren<TerrainCollider>();
			if (terrainCollider != null)
			{
				terrainData = terrainCollider.terrainData;
				Debug.Log("terrainData Found visual mesh");
			}
		}
		else
		{
			var terrain = parentObject.parent.GetComponentInChildren<Terrain>();
			if (terrain != null)
			{
				terrainData = terrain.terrainData;
				Debug.Log("terrainData Found collision mesh");
			}
		}

		if (terrainData == null)
		{
			terrainData = new TerrainData();
			terrainData.name = Path.GetFileNameWithoutExtension(property.uri);

			var texture = new Texture2D(0, 0);

			texture.LoadImage(byteArray, false);
			// Debug.Log("texture = " + texture.width + "," + texture.height + ", " + texture.format + "," + texture.graphicsFormat);

			if (texture.width != texture.height)
			{
				Debug.LogWarningFormat(
					"Width={0}, Height={1}, Texture({2}) is not same!! Not proper for heightmap",
					texture.width, texture.height, property.uri);
			}

			// Configuration order is importatnt
			terrainData.heightmapResolution = Mathf.Max(Mathf.NextPowerOfTwo(texture.width), Mathf.NextPowerOfTwo(texture.height)) + 1;

			var sampling = (int)(property.sampling * MinDetailResolution);
			terrainData.SetDetailResolution(sampling, ResolutionPerPatch);

			var terrainSize = SDF2Unity.GetScale(property.size);
			var DefaultTerrainSize = new Vector3(texture.width, 1f, texture.height);
			terrainData.size = (terrainSize == Vector3.one) ? DefaultTerrainSize : terrainSize;

			UpdateHeightMap(terrainData, texture);
		}

		if (isVisualMesh)
		{
			// terrainData.terrainLayers = new TerrainLayer[property.texture.Count];
			var terrainLayers = new TerrainLayer[property.texture.Count];
			for (var i = 0; i < property.texture.Count; i++)
			{
				var elem = property.texture[i];
				var terrainLayer = new TerrainLayer();

				terrainLayer.name = Path.GetFileNameWithoutExtension(elem.diffuse);
				terrainLayer.diffuseTexture = GenerateTexture(elem.diffuse);
				terrainLayer.normalMapTexture = GenerateTexture(elem.normal);
				terrainLayer.tileSize = (Vector2.one * (int)elem.size);
				terrainLayer.smoothness = DefaultSmootheness;

				// Debug.Log(terrainLayer.name);
				terrainLayers[i] = terrainLayer;
			}

			// TODO: blend texture
			for (var i = 0; i < property.blend.Count; i++)
			{
				var elem = property.blend[i];
				// elem.min_height;
			}

			terrainData.terrainLayers = terrainLayers;

			var terrain = heightmapObject.AddComponent<Terrain>();
			terrain.materialTemplate = new Material(TerrainShader);
			terrain.terrainData = terrainData;
		}
		else
		{
			var terrainCollider = heightmapObject.AddComponent<TerrainCollider>();
			terrainCollider.terrainData = terrainData;
		}
	}
}