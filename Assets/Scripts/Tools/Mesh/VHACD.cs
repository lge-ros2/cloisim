/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public static class VHACD
{
	private static readonly int NumOfLimitConvexMeshTriangles = 255;
	private static readonly float DegenerateThreshold = 1e-4f;

	// Cache decomposition results keyed by source Mesh.
	// Many models share identical meshes (wheels, bolts, repeated parts) — caching avoids
	// recomputing the same convex decomposition multiple times.
	private static readonly Dictionary<Mesh, List<Mesh>> _resultCache = new();

	// libvhacd's thread-safety is not documented/verified, so only one native call
	// runs at a time. This still keeps the (blocking) native work off the main thread.
	private static readonly SemaphoreSlim _vhacdGate = new(1, 1);

	private struct HullData
	{
		public Vector3[] Vertices;
		public int[] Triangles;
	}

	// Local P/Invoke re-declarations of MeshProcess.VHACD's native bindings.
	// The package's declarations are private, and its GenerateConvexMeshes() couples the
	// native call to UnityEngine.Mesh access, which is main-thread only. These duplicate
	// bindings (same native symbols) let us run just the native decomposition on a
	// background thread and keep Mesh construction on the main thread.
	private unsafe struct RawConvexHull
	{
		public double* m_points;
		public uint* m_triangles;
		public uint m_nPoints;
		public uint m_nTriangles;
		public double m_volume;
		public fixed double m_center[3];
	}

	[DllImport("libvhacd")] private static extern unsafe void* CreateVHACD();

	[DllImport("libvhacd")] private static extern unsafe void DestroyVHACD(void* pVHACD);

	[DllImport("libvhacd")]
	private static extern unsafe bool ComputeFloat(
		void* pVHACD,
		float* points,
		uint countPoints,
		uint* triangles,
		uint countTriangles,
		MeshProcess.VHACD.Parameters* parameters);

	[DllImport("libvhacd")] private static extern unsafe uint GetNConvexHulls(void* pVHACD);

	[DllImport("libvhacd")]
	private static extern unsafe void GetConvexHull(void* pVHACD, uint index, RawConvexHull* ch);

	private static unsafe List<HullData> ComputeHullsNative(Vector3[] verts, int[] tris, MeshProcess.VHACD.Parameters parameters)
	{
		var vhacd = CreateVHACD();
		try
		{
			fixed (Vector3* pVerts = verts)
			fixed (int* pTris = tris)
			{
				ComputeFloat(vhacd, (float*)pVerts, (uint)verts.Length, (uint*)pTris, (uint)(tris.Length / 3), &parameters);
			}

			var numHulls = GetNConvexHulls(vhacd);
			var hulls = new List<HullData>((int)numHulls);
			for (uint index = 0; index < numHulls; index++)
			{
				RawConvexHull hull;
				GetConvexHull(vhacd, index, &hull);

				var hullVerts = new Vector3[hull.m_nPoints];
				var pComponents = hull.m_points;
				for (var i = 0; i < hullVerts.Length; i++)
				{
					hullVerts[i] = new Vector3((float)pComponents[0], (float)pComponents[1], (float)pComponents[2]);
					pComponents += 3;
				}

				var indices = new int[hull.m_nTriangles * 3];
				Marshal.Copy((System.IntPtr)hull.m_triangles, indices, 0, indices.Length);

				hulls.Add(new HullData { Vertices = hullVerts, Triangles = indices });
			}
			return hulls;
		}
		finally
		{
			DestroyVHACD(vhacd);
		}
	}

	private static Task<List<HullData>> ComputeHullsAsync(Vector3[] verts, int[] tris)
	{
		var parameters = Params;
		return Task.Run(() =>
		{
			_vhacdGate.Wait();
			try
			{
				return ComputeHullsNative(verts, tris, parameters);
			}
			finally
			{
				_vhacdGate.Release();
			}
		});
	}

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

	public static IEnumerator ApplyAsync(MeshFilter[] meshFilters)
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
				List<Mesh> colliderMeshes;
				if (_resultCache.TryGetValue(mesh, out var cached))
				{
					colliderMeshes = cached;
				}
				else
				{
					var verts = mesh.vertices;
					var tris = mesh.triangles;
					var task = ComputeHullsAsync(verts, tris);
					while (!task.IsCompleted)
					{
						yield return null;
					}

					var hullDataList = task.Result;
					colliderMeshes = new List<Mesh>(hullDataList.Count);
					foreach (var hullData in hullDataList)
					{
						var hullMesh = new Mesh();
						hullMesh.SetVertices(hullData.Vertices);
						hullMesh.SetTriangles(hullData.Triangles, 0);
						colliderMeshes.Add(hullMesh);
					}
					_resultCache[mesh] = colliderMeshes;
				}

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