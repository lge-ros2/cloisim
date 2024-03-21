/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using MeshVHACD = MeshProcess.VHACD;

public partial class VHACD
{
	private static readonly int NumOfLimitConvexMeshTriangles = 255;

	private static MeshVHACD.Parameters VHACDParams = new MeshVHACD.Parameters()
	{
		m_resolution = 18000,
		// m_resolution = 800000, // max: 64,000,000
		m_concavity = 0.005,
		m_planeDownsampling = 3,
		m_convexhullDownsampling = 3,
		m_alpha = 0.1,
		m_beta = 0.05,
		m_pca = 0,
		m_mode = 0,
		m_maxNumVerticesPerCH = 128,
		m_minVolumePerCH = 0.0005,
		m_convexhullApproximation = 1,
		m_oclAcceleration = 0,
		m_maxConvexHulls = 256,
		m_projectHullVertices = true
	};

	public static void Apply(in GameObject targetObject)
	{
		for (var i = 0; i < targetObject.transform.childCount; i++)
		{
			var targetMeshObject = targetObject.transform.GetChild(i).gameObject;

			var meshFilters = targetMeshObject.GetComponentsInChildren<MeshFilter>();

			var decomposer = targetMeshObject.AddComponent<MeshVHACD>();
			decomposer.m_parameters = VHACDParams;

			foreach (var meshFilter in meshFilters)
			{
				// Just skip if the number of vertices in the mesh is less than the limit of convex mesh triangles
				if (meshFilter.sharedMesh.vertexCount >= NumOfLimitConvexMeshTriangles)
				{
					// #if ENABLE_MERGE_COLLIDER
					// 	Debug.LogFormat("Apply VHACD({0}), EnableMergeCollider will be ignored.", targetObject.name);
					// #else
					// 	Debug.LogFormat("Apply VHACD({0})", targetObject.name);
					// #endif
					var colliderMeshes = decomposer.GenerateConvexMeshes(meshFilter.sharedMesh);

					for (var index = 0; index < colliderMeshes.Count; index++)
					{
						var collider = colliderMeshes[index];

						var currentMeshCollider = targetMeshObject.AddComponent<MeshCollider>();
						collider.name = "VHACD_" + meshFilter.name + "_" + index;
						// Debug.Log(collider.name);
						currentMeshCollider.sharedMesh = collider;
						currentMeshCollider.convex = false;
						currentMeshCollider.cookingOptions = SDF.Implement.Collision.CookingOptions;
						currentMeshCollider.hideFlags |= HideFlags.NotEditable;
					}
				}
				else
				{
					var meshCollider = targetMeshObject.AddComponent<MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					meshCollider.convex = false;
					meshCollider.cookingOptions = SDF.Implement.Collision.CookingOptions;
					meshCollider.hideFlags |= HideFlags.NotEditable;
				}
				GameObject.Destroy(meshFilter.gameObject);
			}

			Component.Destroy(decomposer);
		}
	}
}