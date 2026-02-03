/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

public sealed class PluginStartTracker
{
	private readonly HashSet<CLOiSimPlugin> _all = new();
	private readonly HashSet<CLOiSimPlugin> _started = new();
	private readonly StringBuilder _startSummaries = new();
	private bool _allStartedFired = false;

	public int TotalCount => _all.Count;
	public int StartedCount => _started.Count;
	public bool AllStarted => TotalCount == 0 || StartedCount == TotalCount;
	public string AllSummaries => _startSummaries.ToString();

	public event Action<int, int> ProgressChanged;  // started, total
	public event Action AllStartedEvent;

	public void Bind(GameObject modelRoot)
	{
		Clear();

		var plugins = modelRoot.GetComponentsInChildren<CLOiSimPlugin>(includeInactive: true);

		foreach (var p in plugins)
		{
			if (p == null)
				continue;

			_all.Add(p);

			if (p.IsStarted)
				_started.Add(p);
			else
				p.Started += OnPluginStarted;
		}

		ProgressChanged?.Invoke(StartedCount, TotalCount);
		TryFireAllStarted();
	}

	private void OnPluginStarted(CLOiSimPlugin plugin)
	{
		if (plugin == null)
			return;

		if (!_all.Contains(plugin))
			return;

		if (_started.Add(plugin))
		{
			_startSummaries.Append(plugin.StartSummary);
			ProgressChanged?.Invoke(StartedCount, TotalCount);
			TryFireAllStarted();
		}
	}

	private void TryFireAllStarted()
	{
		if (!AllStarted)
			return;

		if (_allStartedFired)
			return;

		_allStartedFired = true;
		AllStartedEvent?.Invoke();
	}

	public void Clear()
	{
		foreach (var p in _all)
		{
			if (p != null)
				p.Started -= OnPluginStarted;
		}
		_all.Clear();
		_started.Clear();
		_startSummaries.Clear();
		_allStartedFired = false;
	}
}