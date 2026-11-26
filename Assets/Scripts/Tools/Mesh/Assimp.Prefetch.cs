/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Parallel look-ahead parsing of mesh files.
/// The SDF importer announces the meshes it is going to load (in import order) via BeginPrefetch();
/// worker threads parse them ahead of the import coroutine and CreateMeshObject() picks up the
/// finished (or still running) parse instead of starting its own.
/// All bookkeeping is main-thread only; worker threads only run GetScene() (no UnityEngine API
/// besides thread-safe Debug.Log).
/// Parse workers: CLOISIM_MESH_PARSE_JOBS (default max(1, ProcessorCount / 2)).
/// Look-ahead: at most 2 x workers parsed-but-unconsumed scenes.
/// </summary>
public static partial class MeshLoader
{
	private sealed class PrefetchEntry
	{
		public string Key;
		public string Path;
		public string SubMesh;
		public int Index;
		public int Refs;
		public Task<Assimp.Scene> Task;
	}

	private static readonly Dictionary<string, PrefetchEntry> _prefetchByKey = new();
	private static readonly List<PrefetchEntry> _prefetchQueue = new();
	private static int _prefetchNext;
	private static int _prefetchCursor;
	private static int _prefetchWindow;
	private static SemaphoreSlim _prefetchGate;
	private static CancellationTokenSource _prefetchCts;

	private static string MakeMeshKey(string meshPath, string subMesh)
	{
		return MeshCacheVersion + ":" + meshPath + (string.IsNullOrEmpty(subMesh) ? "" : subMesh);
	}

	private static int ReadParseJobs()
	{
		var defaultValue = Math.Max(1, Environment.ProcessorCount / 2);
		var text = Environment.GetEnvironmentVariable("CLOISIM_MESH_PARSE_JOBS");
		if (!string.IsNullOrEmpty(text) && int.TryParse(text, out var value))
		{
			return Math.Max(1, value);
		}
		return defaultValue;
	}

	/// <summary>
	/// Starts parsing the given meshes in the given order (main thread only).
	/// Replaces any previous prefetch session. Pair with EndPrefetch().
	/// </summary>
	public static void BeginPrefetch(IReadOnlyList<(string uri, string subMesh)> meshes)
	{
		EndPrefetch();

		if (meshes == null || meshes.Count == 0)
		{
			return;
		}

		foreach (var (uri, subMesh) in meshes)
		{
			if (string.IsNullOrEmpty(uri))
			{
				continue;
			}

			var key = MakeMeshKey(uri, subMesh);

			// Already converted by an earlier load: CreateMeshObject() will not parse it again.
			if (IsMeshCached(key))
			{
				continue;
			}

			if (_prefetchByKey.TryGetValue(key, out var existing))
			{
				existing.Refs++;
				continue;
			}

			var entry = new PrefetchEntry()
			{
				Key = key,
				Path = uri,
				SubMesh = subMesh,
				Index = _prefetchQueue.Count,
				Refs = 1
			};
			_prefetchByKey.Add(key, entry);
			_prefetchQueue.Add(entry);
		}

		if (_prefetchQueue.Count == 0)
		{
			return;
		}

		var workers = ReadParseJobs();
		_prefetchWindow = workers * 2;
		_prefetchGate = new SemaphoreSlim(workers, workers);
		_prefetchCts = new CancellationTokenSource();
		PumpPrefetch();
	}

	/// <summary>Drops all pending prefetches and cancels parses that have not started yet.</summary>
	public static void EndPrefetch()
	{
		if (_prefetchCts != null)
		{
			_prefetchCts.Cancel();
		}

		_prefetchCts = null;
		_prefetchGate = null;
		_prefetchByKey.Clear();
		_prefetchQueue.Clear();
		_prefetchNext = 0;
		_prefetchCursor = 0;
		_prefetchWindow = 0;
	}

	private static void StartPrefetchEntry(PrefetchEntry entry)
	{
		var gate = _prefetchGate;
		var token = _prefetchCts.Token;
		var path = entry.Path;
		var subMesh = entry.SubMesh;

		entry.Task = Task.Run(async () =>
		{
			// Bounds the number of concurrent parses; FIFO, so import order is kept.
			await gate.WaitAsync(token).ConfigureAwait(false);
			try
			{
				token.ThrowIfCancellationRequested();
				return GetScene(path, subMesh);
			}
			finally
			{
				gate.Release();
			}
		}, token);
	}

	// Starts further parses as long as fewer than _prefetchWindow scenes ahead of the consumer are outstanding.
	private static void PumpPrefetch()
	{
		if (_prefetchCts == null)
		{
			return;
		}

		if (_prefetchNext < _prefetchCursor)
		{
			_prefetchNext = _prefetchCursor;
		}

		while (_prefetchNext < _prefetchQueue.Count)
		{
			var entry = _prefetchQueue[_prefetchNext];
			if (entry.Task != null || entry.Refs <= 0)
			{
				_prefetchNext++;
				continue;
			}

			var outstanding = 0;
			for (var index = _prefetchCursor; index < _prefetchNext; index++)
			{
				var other = _prefetchQueue[index];
				if (other.Task != null && other.Refs > 0)
				{
					outstanding++;
				}
			}

			if (outstanding >= _prefetchWindow)
			{
				break;
			}

			StartPrefetchEntry(entry);
			_prefetchNext++;
		}
	}

	private static void ConsumePrefetchEntry(PrefetchEntry entry, bool allOccurrences)
	{
		entry.Refs = allOccurrences ? 0 : entry.Refs - 1;
		if (entry.Refs <= 0)
		{
			_prefetchByKey.Remove(entry.Key);
		}

		if (entry.Index + 1 > _prefetchCursor)
		{
			_prefetchCursor = entry.Index + 1;
		}

		PumpPrefetch();
	}

	/// <summary>
	/// Returns the prefetched parse for the key (starting it right away if the look-ahead has not
	/// reached it yet), or null when nothing was announced for it. Main thread only.
	/// </summary>
	private static Task<Assimp.Scene> TakePrefetchedScene(string key)
	{
		if (_prefetchCts == null || !_prefetchByKey.TryGetValue(key, out var entry))
		{
			return null;
		}

		if (entry.Task == null)
		{
			StartPrefetchEntry(entry);
		}

		var task = entry.Task;

		// With the mesh cache the converted object is cached by the first consumer, later ones never parse.
		ConsumePrefetchEntry(entry, MeshCacheEnabled);
		return task;
	}

	/// <summary>Tells the prefetcher that one announced occurrence is served without a parse (e.g. cache hit).</summary>
	private static void ReleasePrefetchedScene(string key)
	{
		if (_prefetchCts != null && _prefetchByKey.TryGetValue(key, out var entry))
		{
			ConsumePrefetchEntry(entry, false);
		}
	}
}
