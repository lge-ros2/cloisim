/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// TODO(load-profile): temporary instrumentation to find where world loading spends time; remove after

using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UE = UnityEngine;

/// <summary>
/// Main-thread accumulator of wall time and elapsed frames per named load section.
/// Sections may nest, so values are not additive across keys.
/// </summary>
public static class LoadProfiler
{
	private sealed class Section
	{
		public long Ms;
		public long Frames;
		public long Count;
	}

	public struct Scope
	{
		private readonly string _key;
		private readonly long _startTicks;
		private readonly int _startFrame;

		public Scope(string key)
		{
			_key = key;
			_startTicks = Stopwatch.GetTimestamp();
			_startFrame = UE.Time.frameCount;
		}

		public void End()
		{
			var ms = (Stopwatch.GetTimestamp() - _startTicks) * 1000 / Stopwatch.Frequency;
			Add(_key, ms, UE.Time.frameCount - _startFrame);
		}
	}

	private static readonly Dictionary<string, Section> _sections = new();
	private static readonly List<string> _order = new();

	public static Scope Begin(string key) => new Scope(key);

	public static void Add(string key, long ms, int frames)
	{
		if (!_sections.TryGetValue(key, out var section))
		{
			section = new Section();
			_sections.Add(key, section);
			_order.Add(key);
		}
		section.Ms += ms;
		section.Frames += frames;
		section.Count++;
	}

	public static void LogAndReset(string title)
	{
		if (_order.Count == 0)
		{
			return;
		}

		var sb = new StringBuilder();
		sb.Append("[LoadProfile] ").Append(title);
		foreach (var key in _order)
		{
			var s = _sections[key];
			sb.Append("\n  ").Append(key).Append(": ").Append(s.Ms).Append("ms, ")
				.Append(s.Frames).Append(" frames, x").Append(s.Count);
		}
		UE.Debug.Log(sb.ToString());

		_sections.Clear();
		_order.Clear();
	}
}
