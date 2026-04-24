/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.IO;
using System.Drawing;
using System;

public static class ProceduralHeightmap
{
	private static readonly string terrainShaderName = "Universal Render Pipeline/Terrain/Lit";
	private static readonly Shader TerrainShader = Shader.Find(terrainShaderName);

	private static readonly int DetailResolutionRate = 2;
	private static readonly int MinDetailResolution = 8;
	private static readonly float DefaultSmootheness = 0.3f;
	private static readonly float BaseMapDistance = 100f;

	private static void UpdateHeightMap(this TerrainData terrainData, in Texture2D heightmapTexture)
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
		terrainData.SyncHeightmap();
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

	public static void GenerateHeightMap(this GameObject heightmapObject, in SDFormat.Heightmap property, in bool isVisualMesh)
	{
		var byteArray = GetBytesFromImage(property.Uri);
		if (byteArray == null)
		{
			return;
		}

		var parentObject = heightmapObject.transform.parent;

		TerrainData terrainData = null;

		if (isVisualMesh)
		{
			var terrainCollider = parentObject.parent.GetComponentInChildren<TerrainCollider>();
			if (terrainCollider != null)
			{
				terrainData = terrainCollider.terrainData;
			}
		}
		else
		{
			var terrain = parentObject.parent.GetComponentInChildren<Terrain>();
			if (terrain != null)
			{
				terrainData = terrain.terrainData;
			}
		}

		if (terrainData == null)
		{
			terrainData = new TerrainData();
			terrainData.name = Path.GetFileNameWithoutExtension(property.Uri);

			var texture = new Texture2D(0, 0);

			texture.LoadImage(byteArray, false);

			if (texture.width != texture.height)
			{
				Debug.LogWarningFormat(
					"Width={0}, Height={1}, Texture({2}) is not same!! Not proper for heightmap",
					texture.width, texture.height, property.Uri);
			}

			terrainData.heightmapResolution = Mathf.Max(Mathf.NextPowerOfTwo(texture.width), Mathf.NextPowerOfTwo(texture.height)) + 1;

			var sampling = (int)(property.Sampling * MinDetailResolution);
			terrainData.SetDetailResolution(sampling * DetailResolutionRate, sampling);

			terrainData.UpdateHeightMap(texture);

			var terrainSize = SDF2Unity.Scale(property.Size);
			var DefaultTerrainSize = new Vector3(texture.width, 1f, texture.height);
			terrainData.size = (terrainSize == Vector3.one) ? DefaultTerrainSize : terrainSize;
		}

		if (isVisualMesh)
		{
			var terrainLayers = new TerrainLayer[property.Textures.Count];
			for (var i = 0; i < property.Textures.Count; i++)
			{
				var elem = property.Textures[i];
				var terrainLayer = new TerrainLayer();

				terrainLayer.name = Path.GetFileNameWithoutExtension(elem.Diffuse);
				terrainLayer.diffuseTexture = MeshLoader.GetTexture(elem.Diffuse);
				terrainLayer.normalMapTexture = MeshLoader.GetTexture(elem.Normal);
				terrainLayer.tileSize = Vector2.one * (int)elem.Size;
				terrainLayer.smoothness = DefaultSmootheness;

				terrainLayers[i] = terrainLayer;
			}

			for (var i = 0; i < property.Blends.Count; i++)
			{
				var elem = property.Blends[i];
				// elem.MinHeight;
			}

			terrainData.terrainLayers = terrainLayers;

			var terrain = heightmapObject.AddComponent<Terrain>();
			terrain.materialTemplate = new Material(TerrainShader);
			terrain.materialTemplate.hideFlags = HideFlags.DontUnloadUnusedAsset;
			terrain.terrainData = terrainData;
			terrain.drawInstanced = true;
			terrain.basemapDistance = BaseMapDistance;
			terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
		}
		else
		{
			var terrainCollider = heightmapObject.AddComponent<TerrainCollider>();
			terrainCollider.terrainData = terrainData;
		}

		heightmapObject.tag = "Geometry";
	}
}