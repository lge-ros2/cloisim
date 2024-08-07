/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public static class MeshUtil
{
	public static Mesh GetMesh(this TerrainCollider collider)
	{
		if (collider == null)
		{
			return null;
		}

		Mesh mesh = new Mesh();
		var terrainData = collider.terrainData;

		var resolution = terrainData.heightmapResolution;
		Debug.Log(resolution);
		Vector3[] vertices = new Vector3[resolution * resolution];

		// Populate the vertices array with the corresponding height values
		for (int x = 0; x < resolution; x++)
		{
			for (int z = 0; z < resolution; z++)
			{
				float height = terrainData.GetHeight(x, z);
				vertices[x + z * resolution] = new Vector3(x, height, z);
			}
		}

		// Define the mesh's triangles
		int[] triangles = new int[(resolution - 1) * (resolution - 1) * 6];

		// Populate the triangles array with the corresponding triangle indices
		int triangleIndex = 0;
		for (int x = 0; x < resolution - 1; x++)
		{
			for (int z = 0; z < resolution - 1; z++)
			{
				int v0 = x + z * resolution;
				int v1 = x + (z + 1) * resolution;
				int v2 = (x + 1) + z * resolution;
				int v3 = (x + 1) + (z + 1) * resolution;

				triangles[triangleIndex] = v0;
				triangles[triangleIndex + 1] = v1;
				triangles[triangleIndex + 2] = v2;

				triangles[triangleIndex + 3] = v2;
				triangles[triangleIndex + 4] = v1;
				triangles[triangleIndex + 5] = v3;

				triangleIndex += 6;
			}
		}

		// Assign the mesh data to the Mesh object
		mesh.vertices = vertices;
		mesh.triangles = triangles;

		// Optional: Calculate normals and UVs
		mesh.Optimize();
		// mesh.RecalculateNormals();
		// mesh.RecalculateTangents();

		return mesh;
	}
}