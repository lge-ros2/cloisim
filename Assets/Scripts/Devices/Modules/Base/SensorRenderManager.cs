/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

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

	private readonly List<SensorEntry> _entries = new();
	private readonly List<(ISensorRenderable sensor, float initialDelay)> _pendingAdd = new();
	private readonly List<ISensorRenderable> _pendingRemove = new();

	/// <summary>
	/// Register a sensor with an optional initial delay before the first render.
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

		inst._pendingAdd.Add((sensor, initialDelay));
	}

	public static void Unregister(ISensorRenderable sensor)
	{
		Instance?._pendingRemove.Add(sensor);
	}

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

		// If too far behind (> 3 periods), snap forward to avoid burst catch-up
		if (entry.NextRenderTime < realtimeNow - period * 3f)
			entry.NextRenderTime = realtimeNow + period;
	}

	private void LateUpdate()
	{
		ApplyPendingAdditions();
		ApplyPendingRemovals();

		var realtimeNow = Time.realtimeSinceStartup;

		// Sort by urgency (most overdue first)
		_entries.Sort((a, b) =>
		{
			var urgA = realtimeNow - a.NextRenderTime;
			var urgB = realtimeNow - b.NextRenderTime;
			return urgB.CompareTo(urgA);
		});

		// Render all ready sensors and advance their schedules
		for (var i = 0; i < _entries.Count; i++)
		{
			var entry = _entries[i];
			if (!entry.Sensor.CanRender || (realtimeNow < entry.NextRenderTime))
				continue;

			entry.Sensor.ExecuteRenderStep(realtimeNow);

			AdvanceSensorSchedule(ref entry, realtimeNow);

			_entries[i] = entry;
		}
	}

	private void OnApplicationQuit()
	{
		s_applicationQuitting = true;
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
