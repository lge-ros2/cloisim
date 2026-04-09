/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class VHACD
{
	private static readonly int NumOfLimitConvexMeshTriangles = 255;

	public static MeshProcess.VHACD.Parameters Params = new MeshProcess.VHACD.Parameters()
	{
		m_resolution = 100000,
		m_concavity = 0.01,
		m_planeDownsampling = 6,
		m_convexhullDownsampling = 6,
		m_alpha = 0.05,
		m_beta = 0.05,
		m_pca = 0,
		m_mode = 0,
		m_maxNumVerticesPerCH = 64,
		m_minVolumePerCH = 0.003,
		m_convexhullApproximation = 1,
		m_oclAcceleration = 1,
		m_maxConvexHulls = 64,
		m_projectHullVertices = true
	};

	public static void Apply(MeshFilter[] meshFilters)
	{
		var decomposer = Main.MeshVHACD;

		foreach (var meshFilter in meshFilters)
		{
			// Just skip if the number of vertices in the mesh is less than the limit of convex mesh triangles
			if (meshFilter.sharedMesh.vertexCount >= NumOfLimitConvexMeshTriangles)
			{
#if UNITY_EDITOR
#if ENABLE_MERGE_COLLIDER
				Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{meshFilter.sharedMesh.name}) -> {meshFilter.sharedMesh.vertexCount}, EnableMergeCollider will be ignored.");
#else
				Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{meshFilter.sharedMesh.name}) -> {meshFilter.sharedMesh.vertexCount}");
#endif
#endif

				var colliderMeshes = decomposer.GenerateConvexMeshes(meshFilter.sharedMesh);

				for (var index = 0; index < colliderMeshes.Count; index++)
				{
					var colliderMesh = colliderMeshes[index];

					var currentMeshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
					colliderMesh.name = "VHACD_" + meshFilter.name + "_" + index;

					currentMeshCollider.sharedMesh = colliderMesh;
					currentMeshCollider.convex = true;
					currentMeshCollider.cookingOptions = SDF.Implement.Collision.CookingOptions;
					currentMeshCollider.hideFlags |= HideFlags.NotEditable;
				}
			}
			else
			{
				var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
				meshCollider.sharedMesh = meshFilter.sharedMesh;
				meshCollider.convex = true;
				meshCollider.cookingOptions = SDF.Implement.Collision.CookingOptions;
				meshCollider.hideFlags |= HideFlags.NotEditable;
			}
		}
	}
}