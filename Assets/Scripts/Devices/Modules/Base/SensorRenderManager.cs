/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Central manager that owns render scheduling for all ISensorRenderable sensors.
/// Determines when each sensor should render based on its RenderPeriod,
/// sorts by urgency, and dispatches ExecuteRenderStep in a tight batch each frame.
/// </summary>
public class SensorRenderManager : MonoBehaviour
{
	private static SensorRenderManager s_instance;
	private static bool s_applicationQuitting = false;

	/// <summary>Per-sensor scheduling state owned by the manager.</summary>
	private struct SensorEntry
	{
		public ISensorRenderable Sensor;
		public float NextRenderTime;
	}

	public static SensorRenderManager Instance
	{
		get
		{
			if (s_applicationQuitting)
				return null;

			if (s_instance == null)
			{
				s_instance = Main.Core.AddComponent<SensorRenderManager>();
			}
			return s_instance;
		}
	}

	/// <summary>
	/// Maximum number of rasterization-based (non-URT) sensor renders allowed
	/// per frame. This prevents frame spikes when many cameras become due simultaneously.
	/// </summary>
	private const int MaxRasterRendersPerFrame = 2;

	/// <summary>
	/// Maximum number of URT (compute dispatch + AsyncGPUReadback) sensor renders
	/// allowed per frame. Without a cap, simultaneous readback completions cause
	/// callback storms in EarlyUpdate.UpdateAsyncReadbackManager (40ms+ spikes).
	/// Keep this low (1-2) because readback callbacks from prior frames can still
	/// cluster even if dispatches are spread out.
	/// </summary>
	private const int MaxURTRendersPerFrame = 2;

	/// <summary>Number of distinct jitter slots used to stagger sensor schedules.</summary>
	private const int JitterSlotCount = 7;

	/// <summary>Time offset (seconds) between adjacent jitter slots.</summary>
	private const float JitterStepSeconds = 0.005f;

	/// <summary>
	/// If a sensor falls behind by more than this many periods, its schedule
	/// is snapped forward instead of bursting through all missed renders.
	/// </summary>
	private const float MaxCatchUpPeriods = 3f;

	private readonly List<SensorEntry> _entries = new();
	private readonly List<(ISensorRenderable sensor, float initialDelay)> _pendingAdd = new();
	private readonly List<ISensorRenderable> _pendingRemove = new();

	private bool _paused = false;

	private static int s_registrationCounter = 0;

	/// <summary>
	/// Register a sensor with an optional initial delay before the first render.
	/// A small per-sensor jitter is added to prevent schedule synchronization
	/// that causes periodic readback callback storms.
	/// </summary>
	public static void Register(ISensorRenderable sensor, float initialDelay = 0.1f)
	{
		var inst = Instance;
		if (inst == null) return;

		// Avoid duplicates
		foreach (var e in inst._entries)
			if (e.Sensor == sensor) return;
		foreach (var p in inst._pendingAdd)
			if (p.sensor == sensor) return;

		// Stagger sensors by adding a small offset based on registration order.
		// This prevents all sensors from becoming due on the same frame.
		var jitter = s_registrationCounter++ % JitterSlotCount * JitterStepSeconds;
		inst._pendingAdd.Add((sensor, initialDelay + jitter));
	}

	public static void Unregister(ISensorRenderable sensor)
	{
		Instance?._pendingRemove.Add(sensor);
	}

	/// <summary>
	/// Pause all sensor rendering (e.g. during simulation reset).
	/// </summary>
	public static void Pause() { if (Instance != null) Instance._paused = true; }

	/// <summary>
	/// Resume sensor rendering after a pause.
	/// </summary>
	public static void Resume() { if (Instance != null) Instance._paused = false; }

	private void ApplyPendingAdditions()
	{
		if (_pendingAdd.Count > 0)
		{
			var now = Time.realtimeSinceStartup;
			foreach (var (sensor, delay) in _pendingAdd)
			{
				_entries.Add(new SensorEntry
				{
					Sensor = sensor,
					NextRenderTime = now + delay,
				});
			}
			_pendingAdd.Clear();
		}
	}

	private void ApplyPendingRemovals()
	{
		if (_pendingRemove.Count > 0)
		{
			foreach (var s in _pendingRemove)
				_entries.RemoveAll(e => e.Sensor == s);
			_pendingRemove.Clear();
		}
	}

	private void AdvanceSensorSchedule(ref SensorEntry entry, float realtimeNow)
	{
		// Advance schedule by one period
		var period = entry.Sensor.RenderPeriod;
		entry.NextRenderTime += period;

		// If too far behind, snap forward to avoid burst catch-up
		if (entry.NextRenderTime < realtimeNow - period * MaxCatchUpPeriods)
			entry.NextRenderTime = realtimeNow + period;
	}

	private void LateUpdate()
	{
		ApplyPendingAdditions();
		ApplyPendingRemovals();

		if (_paused)
			return;

		// A huge frame hitch usually means a heavy synchronous SDF/mesh import just ran
		// on the main thread (e.g. importing a very-high-poly robot). The GPU uploads it
		// triggered may still be settling; submitting a sensor render immediately after
		// has been observed to crash natively inside URP's ScriptableRenderContext.Submit.
		// Skip one beat so those uploads have a frame to land before we submit our own.
		if (CLOiSim.Diagnostics.FreezeWatchdog.RecentlyHadBigHitch())
			return;

		var realtimeNow = Time.realtimeSinceStartup;

		// Sort by urgency (most overdue first). "realtimeNow - NextRenderTime" descending
		// is equivalent to NextRenderTime ascending (realtimeNow cancels out in the
		// comparison), which lets this run every frame without allocating a new closure —
		// a lambda with no captured locals is cached by the compiler as a static delegate.
		_entries.Sort((a, b) => a.NextRenderTime.CompareTo(b.NextRenderTime));

		// Render ready sensors with a per-frame budget for both raster and URT renders.
		// This prevents AsyncGPUReadback callback storms when many sensors fire together.
		var rasterRenderCount = 0;
		var urtRenderCount = 0;
		for (var i = 0; i < _entries.Count; i++)
		{
			var entry = _entries[i];
			if (!entry.Sensor.CanRender || (realtimeNow < entry.NextRenderTime))
				continue;

			// Enforce budget for both sensor types.
			// A sensor skipped for budget keeps its NextRenderTime unchanged (do not
			// advance it here) so it becomes strictly more overdue — and thus more
			// urgent in next frame's sort — until it wins a budget slot. Advancing it
			// like a normal render would freeze the relative urgency ordering forever,
			// letting the same sensors win the budget every contested frame while
			// others starve permanently.
			if (entry.Sensor.IsURT)
			{
				if (urtRenderCount >= MaxURTRendersPerFrame)
					continue;
				urtRenderCount++;
			}
			else
			{
				if (rasterRenderCount >= MaxRasterRendersPerFrame)
					continue;
				rasterRenderCount++;
			}

			// Isolate per-sensor faults: a throw from one sensor's render step must
			// not abort the loop, or every sensor sorted after it is starved this
			// frame — and if the fault repeats, those feeds freeze permanently.
			try
			{
				entry.Sensor.ExecuteRenderStep(realtimeNow);
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[SensorRenderManager] ExecuteRenderStep threw; skipping this sensor: {e.Message}");
			}

			AdvanceSensorSchedule(ref entry, realtimeNow);

			_entries[i] = entry;
		}
	}

	private void OnApplicationQuit()
	{
		s_applicationQuitting = true;
		_paused = true;

		// Drain all in-flight AsyncGPUReadback requests before any sensor
		// OnDestroy runs. Without this, GPU resources freed in OnDestroy
		// can be accessed by the GfxDevice thread → SIGSEGV.
		// (skips the blocking wait entirely when nothing is in flight)
		Device.DrainReadbacksForTeardown();
	}

	private void OnDestroy()
	{
		_entries.Clear();
		_pendingAdd.Clear();
		_pendingRemove.Clear();

		if (s_instance == this)
			s_instance = null;
	}
}
