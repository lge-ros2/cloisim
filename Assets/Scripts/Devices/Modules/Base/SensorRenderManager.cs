/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager that renders all ISensorRenderable cameras in a tight batch
/// each frame. Replaces per-camera CameraWorker coroutines to reduce CPU
/// overhead by allowing the render pipeline to share state across sequential
/// renders.
/// </summary>
public class SensorRenderManager : MonoBehaviour
{
	private static SensorRenderManager s_instance;
	private static bool s_applicationQuitting = false;

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

	private readonly List<ISensorRenderable> _sensors = new();
	private readonly List<ISensorRenderable> _pendingAdd = new();
	private readonly List<ISensorRenderable> _pendingRemove = new();

	public static void Register(ISensorRenderable sensor)
	{
		if (!Instance._sensors.Contains(sensor) &&
			!Instance._pendingAdd.Contains(sensor))
		{
			Instance._pendingAdd.Add(sensor);
		}
	}

	public static void Unregister(ISensorRenderable sensor)
	{
		Instance?._pendingRemove.Add(sensor);
	}

	private void LateUpdate()
	{
		// Apply pending additions/removals
		if (_pendingAdd.Count > 0)
		{
			_sensors.AddRange(_pendingAdd);
			_pendingAdd.Clear();
		}

		if (_pendingRemove.Count > 0)
		{
			foreach (var s in _pendingRemove)
				_sensors.Remove(s);
			_pendingRemove.Clear();
		}

		var now = Time.realtimeSinceStartup;

		// Sort by urgency (most overdue first)
		_sensors.Sort((a, b) => b.GetRenderUrgency(now).CompareTo(a.GetRenderUrgency(now)));

		// Render all ready sensors in a tight batch
		for (var i = 0; i < _sensors.Count; i++)
		{
			var sensor = _sensors[i];
			if (sensor.IsReadyToRender(now))
			{
				sensor.ExecuteRenderStep(now);
			}
		}
	}

	private void OnApplicationQuit()
	{
		s_applicationQuitting = true;
	}

	private void OnDestroy()
	{
		_sensors.Clear();
		_pendingAdd.Clear();
		_pendingRemove.Clear();

		if (s_instance == this)
			s_instance = null;
	}
}
