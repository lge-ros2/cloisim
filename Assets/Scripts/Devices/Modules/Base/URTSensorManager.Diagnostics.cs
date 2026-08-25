/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

// Diagnostics ring buffer + GPU-fault history dump for the shared URT sensor
// manager. Split out of URTSensorManager.cs to keep the main BVH/fence logic
// smaller; a pure source reorganization with no behavior change.
public partial class URTSensorManager
{
	#region "Diagnostics"

	private const int DiagRingSize = 16;
	private readonly Queue<string> _diagHistory = new(DiagRingSize + 1);
	private int _diagPrevInstanceCount = -1;
	private int _diagPrevScratchHash;

	// Cached from LateUpdate (main thread) so DumpDiagHistory can be called safely
	// from FreezeWatchdog's background thread — Time.frameCount throws a
	// UnityException off the main thread, which would otherwise abort the
	// watchdog's escalation before it reaches Process.Kill().
	private volatile int _cachedFrameCount;

	// Same reasoning as _cachedFrameCount: GraphicsBuffer.IsValid()/count/stride
	// call into native code that requires the main thread (IsValidBuffer_Injected),
	// so DumpDiagHistory must never touch _rtBuildScratchBuffer's properties
	// directly when invoked from the watchdog thread.
	private volatile bool _cachedScratchBufferValid;
	private volatile int _cachedScratchBufferCount;
	private volatile int _cachedScratchBufferStride;

	private void DiagRecord(string entry)
	{
		_diagHistory.Enqueue(entry);
		if (_diagHistory.Count > DiagRingSize)
			_diagHistory.Dequeue();
	}

	/// <summary>
	/// Append a per-sensor diagnostic entry (e.g. buffer recreation with GPU handle
	/// hashes) into the shared URT ring buffer so it appears in DumpDiagHistory when
	/// a "missing UAV"/"incompatible ComputeBuffer" GPU fault is later detected.
	/// </summary>
	public static void DiagLog(string entry)
	{
		s_instance?.DiagRecord(entry);
	}

	/// <summary>
	/// Dump the last DiagRingSize URT state-change events to the log.
	/// Called automatically when a GPU UAV error is detected.
	/// </summary>
	public static void DumpDiagHistory(string trigger)
	{
		var inst = s_instance;
		if (inst == null)
		{
			Debug.LogError("[URT-DIAG] DumpDiagHistory: URTSensorManager instance is null.");
			return;
		}

		var sb = new System.Text.StringBuilder();
		// NOTE: this can be called from FreezeWatchdog's background thread on a stall
		// escalation — use the cached frame count, never Time.frameCount directly
		// (throws UnityException off the main thread and would abort the caller
		// before it reaches its own process-kill fallback).
		sb.AppendLine($"[URT-DIAG] === History dump triggered by: {trigger} (frame={inst._cachedFrameCount}) ===");
		sb.AppendLine($"[URT-DIAG] generation={inst._rtAccelStructGeneration} activeIndex={inst._activeIndex}"
			+ $" structReady=[{inst._structReady[0]},{inst._structReady[1]}]"
			+ $" hasPendingBuild=[{inst._hasPendingBuild[0]},{inst._hasPendingBuild[1]}]"
			+ $" accelNull=[{inst._rtAccelStructs[0] == null},{inst._rtAccelStructs[1] == null}]"
			+ $" frameOfLastBuild={inst._frameOfLastBuild}");
		var bs = inst._rtBuildScratchBuffer;
		// NOTE: use the cached values below, never bs.IsValid()/count/stride directly —
		// those call into native code that requires the main thread and throw when
		// invoked from FreezeWatchdog's background thread (same reasoning as frameCount above).
		sb.AppendLine($"[URT-DIAG] buildScratch: "
			+ (bs == null ? "null" : $"hash=0x{bs.GetHashCode():X} valid={inst._cachedScratchBufferValid} count={inst._cachedScratchBufferCount} stride={inst._cachedScratchBufferStride} (cached)")
			+ $" | deferredScratchFree={inst._deferredScratchFree.Count} deferredDispose={inst._deferredDispose.Count}");
		sb.AppendLine("[URT-DIAG] --- recent state changes (oldest first) ---");
		foreach (var line in inst._diagHistory)
			sb.AppendLine(line);
		sb.AppendLine("[URT-DIAG] === End of history ===");
		Debug.LogError(sb.ToString());
	}

	#endregion
}
