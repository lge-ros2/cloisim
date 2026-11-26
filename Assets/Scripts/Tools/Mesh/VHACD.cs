/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lge.Vhacd;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class VHACD
{
	private static readonly int NumOfLimitConvexMeshTriangles = 255;
	private static readonly float DegenerateThreshold = 1e-4f;

	// Cache decomposition results keyed by source Mesh.
	// Many models share identical meshes (wheels, bolts, repeated parts) — caching avoids
	// recomputing the same convex decomposition multiple times.
	private static readonly Dictionary<Mesh, List<Mesh>> _resultCache = new();

	// In-flight decomposition tasks keyed by source Mesh (main-thread access only).
	// Concurrent ApplyAsync() calls that need the same Mesh join the same task.
	private static readonly Dictionary<Mesh, Task<HullData[]>> _inFlight = new();

	// Limits the number of concurrent native decompositions. V-HACD itself is multi-threaded
	// (asyncACD), so a small default avoids CPU oversubscription.
	private static readonly int MaxConcurrentJobs = ReadEnvInt("CLOISIM_VHACD_JOBS", Mathf.Clamp(Environment.ProcessorCount / 2, 2, 4), 1);
	private static readonly SemaphoreSlim _gate = new(MaxConcurrentJobs, MaxConcurrentJobs);

	private static readonly HullData[] EmptyHulls = new HullData[0];

	// Disk cache (lazily created on the main thread, null when disabled or unavailable).
	private static VhacdCache _diskCache = null;
	private static bool _diskCacheInitialized = false;

	// Statistics (see ConsumeStatsSummary)
	private static long _statDecomposed;
	private static long _statDiskHits;
	private static long _statMemoryHits;
	private static long _statJoins;
	private static long _statFailures;
	private static long _statHulls;
	private static long _statComputeMs;
	private static long _statWaitMs;

	public static VhacdParameters Params = CreateDefaultParams();

	private static VhacdParameters CreateDefaultParams()
	{
		var parameters = VhacdParameters.Default;
		parameters.Resolution = (uint)ReadEnvInt("CLOISIM_VHACD_RESOLUTION", (int)parameters.Resolution, 1);
		parameters.MaxConvexHulls = (uint)ReadEnvInt("CLOISIM_VHACD_MAX_HULLS", (int)parameters.MaxConvexHulls, 1);
		return parameters;
	}

	private static int ReadEnvInt(string name, int defaultValue, int minValue)
	{
		var text = Environment.GetEnvironmentVariable(name);
		if (!string.IsNullOrEmpty(text) && int.TryParse(text, out var value))
		{
			return Mathf.Max(value, minValue);
		}
		return defaultValue;
	}

	private static VhacdCache GetDiskCache()
	{
		if (_diskCacheInitialized)
		{
			return _diskCache;
		}
		_diskCacheInitialized = true;

		try
		{
			if (Environment.GetEnvironmentVariable("CLOISIM_VHACD_CACHE") == "0")
			{
				return null;
			}

			var directory = Environment.GetEnvironmentVariable("CLOISIM_VHACD_CACHE_DIR");
			if (string.IsNullOrEmpty(directory))
			{
				if (Application.platform == RuntimePlatform.WindowsPlayer ||
					Application.platform == RuntimePlatform.WindowsEditor)
				{
					var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
					directory = Path.Combine(localAppData, "cloisim", "vhacd");
				}
				else
				{
					var cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
					if (string.IsNullOrEmpty(cacheHome))
					{
						cacheHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
					}
					directory = Path.Combine(cacheHome, "cloisim", "vhacd");
				}
			}

			_diskCache = new VhacdCache(directory);
			_diskCache.TrimInBackground();
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[VHACD] Disk cache disabled: {e.Message}");
			_diskCache = null;
		}
		return _diskCache;
	}

	public static void ClearCache()
	{
		_resultCache.Clear();
	}

	// Returns one summary line and resets the counters. Empty string if nothing was processed.
	public static string ConsumeStatsSummary()
	{
		var decomposed = Interlocked.Exchange(ref _statDecomposed, 0);
		var diskHits = Interlocked.Exchange(ref _statDiskHits, 0);
		var memoryHits = Interlocked.Exchange(ref _statMemoryHits, 0);
		var joins = Interlocked.Exchange(ref _statJoins, 0);
		var failures = Interlocked.Exchange(ref _statFailures, 0);
		var hulls = Interlocked.Exchange(ref _statHulls, 0);
		var computeMs = Interlocked.Exchange(ref _statComputeMs, 0);
		var waitMs = Interlocked.Exchange(ref _statWaitMs, 0);

		if (decomposed + diskHits + memoryHits + joins + failures + hulls == 0)
		{
			return string.Empty;
		}

		return $"decomposed={decomposed}, disk_hits={diskHits}, memory_hits={memoryHits}, inflight_joins={joins}, " +
			$"failures={failures}, hulls={hulls}, native_compute_sum={computeMs}ms, await_wall={waitMs}ms, jobs={MaxConcurrentJobs}";
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

	// Runs on a background thread: only arrays and Lge.Vhacd are touched here.
	private static Task<HullData[]> StartDecompose(Vector3[] verts, int[] tris, VhacdParameters parameters, VhacdCache cache, string meshName)
	{
		return Task.Run(async () =>
		{
			try
			{
				string key = null;
				if (cache != null)
				{
					key = VhacdCache.ComputeKey(verts, tris, parameters);
					if (cache.TryLoad(key, out var cachedHulls) && cachedHulls != null && cachedHulls.Length > 0)
					{
						Interlocked.Increment(ref _statDiskHits);
						Interlocked.Add(ref _statHulls, cachedHulls.Length);
						return cachedHulls;
					}
				}

				await _gate.WaitAsync().ConfigureAwait(false);
				HullData[] hulls;
				try
				{
					var stopwatch = Stopwatch.StartNew();
					hulls = Vhacd.Decompose(verts, tris, parameters);
					Interlocked.Add(ref _statComputeMs, stopwatch.ElapsedMilliseconds);
				}
				finally
				{
					_gate.Release();
				}

				if (hulls == null || hulls.Length == 0)
				{
					Interlocked.Increment(ref _statFailures);
					return EmptyHulls;
				}

				Interlocked.Increment(ref _statDecomposed);
				Interlocked.Add(ref _statHulls, hulls.Length);

				if (cache != null)
				{
					cache.Store(key, hulls);
				}
				return hulls;
			}
			catch (Exception e)
			{
				Interlocked.Increment(ref _statFailures);
				Debug.LogWarning($"[VHACD] Decomposition failed for '{meshName}': {e.Message}");
				return EmptyHulls;
			}
		});
	}

	private sealed class Entry
	{
		public MeshFilter MeshFilter;
		public Mesh Mesh;
		public bool Decompose;
		public Task<HullData[]> Task;
	}

	public static IEnumerator ApplyAsync(MeshFilter[] meshFilters)
	{
		var entries = new List<Entry>(meshFilters.Length);

		// Pass 1 (main thread): classify and start background decompositions.
		foreach (var meshFilter in meshFilters)
		{
			var mesh = meshFilter.sharedMesh;
			var entry = new Entry() { MeshFilter = meshFilter, Mesh = mesh };
			entries.Add(entry);

			// Skip degenerate/planar meshes that would crash VHACD voxelization
			var size = mesh.bounds.size;
			if (size.x < DegenerateThreshold || size.y < DegenerateThreshold || size.z < DegenerateThreshold)
			{
				Debug.LogWarning($"Skip VHACD({meshFilter.name}): degenerate bounds {size}, using simple MeshCollider");
				continue;
			}

			if (mesh.vertexCount < NumOfLimitConvexMeshTriangles)
			{
				continue;
			}

#if UNITY_EDITOR
#if ENABLE_MERGE_COLLIDER
			Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{mesh.name}) -> {mesh.vertexCount}, EnableMergeCollider will be ignored.");
#else
			Debug.LogFormat($"Apply VHACD({meshFilter.gameObject.name}::{meshFilter.name}::{mesh.name}) -> {mesh.vertexCount}");
#endif
#endif
			entry.Decompose = true;

			if (_resultCache.ContainsKey(mesh))
			{
				Interlocked.Increment(ref _statMemoryHits);
			}
			else if (_inFlight.TryGetValue(mesh, out var running))
			{
				Interlocked.Increment(ref _statJoins);
				entry.Task = running;
			}
			else
			{
				// Mesh access is main-thread only, so read the arrays here.
				var verts = mesh.vertices;
				var tris = mesh.triangles;
				entry.Task = StartDecompose(verts, tris, Params, GetDiskCache(), mesh.name);
				_inFlight[mesh] = entry.Task;
			}
		}

		// Pass 2: wait for all background work, blocking within the frame budget before giving a frame back.
		var waitStopwatch = Stopwatch.StartNew();
		foreach (var entry in entries)
		{
			if (entry.Task == null)
			{
				continue;
			}

			while (!LoadFrameBudget.TryWait(entry.Task))
			{
				yield return null;
			}
		}
		Interlocked.Add(ref _statWaitMs, waitStopwatch.ElapsedMilliseconds);

		// Pass 3 (main thread): build hull meshes and add colliders in the original order.
		foreach (var entry in entries)
		{
			var meshFilter = entry.MeshFilter;
			var mesh = entry.Mesh;

			if (!entry.Decompose)
			{
				meshFilter.gameObject.AddMeshCollider(mesh);
				continue;
			}

			if (!_resultCache.TryGetValue(mesh, out var colliderMeshes))
			{
				var hullDataList = entry.Task.IsFaulted ? EmptyHulls : entry.Task.Result;

				// Consumed: no longer in flight.
				if (_inFlight.TryGetValue(mesh, out var registered) && registered == entry.Task)
				{
					_inFlight.Remove(mesh);
				}

				if (hullDataList.Length == 0)
				{
					Debug.LogWarning($"VHACD({meshFilter.name}): decomposition produced no hulls, using simple MeshCollider");
					meshFilter.gameObject.AddMeshCollider(mesh);
					continue;
				}

				colliderMeshes = new List<Mesh>(hullDataList.Length);
				foreach (var hullData in hullDataList)
				{
					// V-HACD can emit flat zero-volume hulls; they fail convex cooking and add nothing.
					if (hullData.Volume <= 0 || hullData.Vertices.Length < 4)
					{
						continue;
					}

					var hullMesh = new Mesh();
					hullMesh.SetVertices(hullData.Vertices);
					hullMesh.SetTriangles(hullData.Triangles, 0);
					colliderMeshes.Add(hullMesh);
				}
				if (colliderMeshes.Count == 0)
				{
					Debug.LogWarning($"VHACD({meshFilter.name}): only degenerate hulls produced, using simple MeshCollider");
					meshFilter.gameObject.AddMeshCollider(mesh);
					continue;
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
	}
}
