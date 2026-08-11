/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Stopwatch = System.Diagnostics.Stopwatch;
using UnityEngine;

// Editor-only publish-Hz diagnostics and profiler for the Device base class.
// Split out of Device.cs to keep the worker-thread/message-pool logic smaller.
// Pure source reorganization with no behavior change.
public abstract partial class Device
{
#if UNITY_EDITOR
	#region DIAGNOSTICS
	// Per-sensor publish Hz diagnostics
	private readonly Stopwatch _diagPublishSw = new();
	private int _diagPublishCount;
	private float _diagPublishHz;
	private const float DEVICE_DIAG_INTERVAL_SEC = 30f;

	/// <summary>Actual measured publish Hz (updated every DEVICE_DIAG_INTERVAL_SEC).</summary>
	public float PublishHz => _diagPublishHz;
	#endregion

	#region PROFILER
	private int _profFrameCount = 0;
	private double _profByteCount = 0;
	private float _periodForProfiler = 60f; // seconds

	private Stopwatch _profWatch = Stopwatch.StartNew();

	[ContextMenu("Reset Profiler")]
	private void ResetProfiler()
	{
		_profFrameCount = 0;
		_profByteCount = 0;
		_profWatch.Restart();
	}

	protected void UpdateProfiler(in string targetName, in double byteCount)
	{
		const double oneMegabyte = 1024.0 * 1024.0;

		_profFrameCount++;
		_profByteCount += byteCount;
		var seconds = _profWatch.Elapsed.TotalSeconds;
		if (seconds >= _periodForProfiler)
		{
			var hz = _profFrameCount / seconds;
			var mbPerSec = _profByteCount / seconds / oneMegabyte;
			var mbps = _profByteCount * 8.0 / seconds / oneMegabyte;
			Debug.Log($"[PROF][{targetName}] {DeviceName} Hz: {hz:F2} | Bandwidth: {mbps:F2} Mbps ({mbPerSec:F2} MB/s)");
			ResetProfiler();
		}
	}
	#endregion
#endif
}
