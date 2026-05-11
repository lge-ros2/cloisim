/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public static class VHACD
{
	private static readonly int NumOfLimitConvexMeshTriangles = 255;
	private static readonly float DegenerateThreshold = 1e-4f;

	// Cache decomposition results keyed by source Mesh.
	// Many models share identical meshes (wheels, bolts, repeated parts) — caching avoids
	// recomputing the same convex decomposition multiple times.
	private static readonly Dictionary<Mesh, List<Mesh>> _resultCache = new();

	public static MeshProcess.VHACD.Parameters Params = new MeshProcess.VHACD.Parameters()
	{
		m_resolution = 50000,
		m_concavity = 0.05,
		m_planeDownsampling = 8,
		m_convexhullDownsampling = 8,
		m_alpha = 0.05,
		m_beta = 0.05,
		m_pca = 0,
		m_mode = 0,
		m_maxNumVerticesPerCH = 32,
		m_minVolumePerCH = 0.003,
		m_convexhullApproximation = 1,
		m_oclAcceleration = 1,
		m_maxConvexHulls = 32,
		m_projectHullVertices = true
	};

	public static void ClearCache()
	{
		_resultCache.Clear();
	}

	private static MeshCollider AddMeshCollider(this GameObject targetObject, Mesh mesh)
	{
		var meshCollider = targetObject.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = mesh;
		meshCollider.convex = false;
		meshCollider.cookingOptions = SDFormat.Implement.Collision.CookingOptions;
		meshCollider.hideFlags |= HideFlags.NotEditable;
		return meshCollider;
	}

	private static List<Mesh> DecomposeWithCache(this Mesh mesh)
	{
		if (_resultCache.TryGetValue(mesh, out var cached))
		{
			return cached;
		}

		var result = Main.MeshVHACD.GenerateConvexMeshes(mesh);
		_resultCache[mesh] = result;
		return result;
	}

	public static void Apply(MeshFilter[] meshFilters)
	{
		foreach (var meshFilter in meshFilters)
		{
			var mesh = meshFilter.sharedMesh;

			// Skip degenerate/planar meshes that would crash VHACD voxelization
			var size = mesh.bounds.size;
			if (size.x < DegenerateThreshold || size.y < DegenerateThreshold || size.z < DegenerateThreshold)
			{
				Debug.LogWarning($"Skip VHACD({meshFilter.name}): degenerate bounds {size}, using simple MeshCollider");
				meshFilter.gameObject.AddMeshCollider(mesh);
				continue;
			}

			if (mesh.vertexCount >= NumOfLimitConvexMeshTriangles)
			{
#if UNITY_EDITOR
#if ENABLE_MERGE_COLLIDER
				Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{mesh.name}) -> {mesh.vertexCount}, EnableMergeCollider will be ignored.");
#else
				Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{mesh.name}) -> {mesh.vertexCount}");
#endif
#endif
				var colliderMeshes = mesh.DecomposeWithCache();

				for (var index = 0; index < colliderMeshes.Count; index++)
				{
					var colliderMesh = colliderMeshes[index];
					colliderMesh.name = "VHACD_" + meshFilter.name + "_" + index;
					meshFilter.gameObject.AddMeshCollider(colliderMesh);
				}
			}
			else
			{
				meshFilter.gameObject.AddMeshCollider(mesh);
			}
		}
	}
}