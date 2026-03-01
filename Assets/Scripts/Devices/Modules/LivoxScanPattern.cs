/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SensorDevices
{
	/// <summary>
	/// Represents a single Livox ray direction in the scan pattern.
	/// </summary>
	public struct LivoxRayInfo
	{
		/// <summary>Horizontal (azimuth) angle in degrees.</summary>
		public float azimuthDeg;

		/// <summary>Vertical (elevation) angle in degrees. Positive = up.</summary>
		public float elevationDeg;
	}

	/// <summary>
	/// Loads and manages a Livox non-repetitive scan pattern from a CSV file.
	/// CSV format: Time/s, Azimuth/deg, Zenith/deg (header row, then data).
	/// Zenith is measured from vertical: 90° = horizontal, less than 90° = up, greater than 90° = down.
	/// The pattern is cycled through in a rolling window, producing the characteristic
	/// non-repetitive scan pattern of Livox LiDAR sensors.
	/// </summary>
	public class LivoxScanPattern
	{
		private LivoxRayInfo[] _allRays;
		private int _currentIndex;

		/// <summary>Total number of rays in the full scan pattern.</summary>
		public int TotalRayCount => _allRays?.Length ?? 0;

		/// <summary>Current rolling-window start index.</summary>
		public int CurrentIndex
		{
			get => _currentIndex;
			set => _currentIndex = (_allRays != null && _allRays.Length > 0)
				? ((value % _allRays.Length) + _allRays.Length) % _allRays.Length
				: 0;
		}

		/// <summary>Minimum azimuth angle in degrees.</summary>
		public float AzimuthMinDeg { get; private set; }

		/// <summary>Maximum azimuth angle in degrees.</summary>
		public float AzimuthMaxDeg { get; private set; }

		/// <summary>Minimum elevation angle in degrees (most downward).</summary>
		public float ElevationMinDeg { get; private set; }

		/// <summary>Maximum elevation angle in degrees (most upward).</summary>
		public float ElevationMaxDeg { get; private set; }

		/// <summary>Whether the azimuth range covers a full 360°.</summary>
		public bool IsFullRotation { get; private set; }

		/// <summary>
		/// Load a scan pattern from a Unity TextAsset located in Resources/LivoxScanPatterns/.
		/// </summary>
		/// <param name="scanMode">Model name without extension, e.g. "mid360", "avia".</param>
		public static LivoxScanPattern Load(string scanMode)
		{
			var textAsset = Resources.Load<TextAsset>("LivoxScanPatterns/" + scanMode);
			if (textAsset == null)
			{
				Debug.LogError($"[LivoxScanPattern] Failed to load: LivoxScanPatterns/{scanMode}");
				return null;
			}

			var pattern = new LivoxScanPattern();
			pattern.Parse(textAsset.text);
			Resources.UnloadAsset(textAsset);
			return pattern;
		}

		private void Parse(string csvText)
		{
			var lines = csvText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
			var rays = new List<LivoxRayInfo>(lines.Length);

			var azMin = float.MaxValue;
			var azMax = float.MinValue;
			var elMin = float.MaxValue;
			var elMax = float.MinValue;

			// Skip header row (line 0)
			for (var i = 1; i < lines.Length; i++)
			{
				var parts = lines[i].Split(',');
				if (parts.Length < 3)
					continue;

				if (!float.TryParse(parts[1].Trim(), NumberStyles.Float,
					CultureInfo.InvariantCulture, out var azimuthDeg))
					continue;

				if (!float.TryParse(parts[2].Trim(), NumberStyles.Float,
					CultureInfo.InvariantCulture, out var zenithDeg))
					continue;

				// Convert zenith (from vertical pole) to elevation (from horizontal)
				// zenith=90° → elevation=0° (horizontal)
				// zenith=80° → elevation=+10° (up)
				// zenith=100° → elevation=-10° (down)
				var elevationDeg = 90f - zenithDeg;

				rays.Add(new LivoxRayInfo
				{
					azimuthDeg = azimuthDeg,
					elevationDeg = elevationDeg
				});

				if (azimuthDeg < azMin) azMin = azimuthDeg;
				if (azimuthDeg > azMax) azMax = azimuthDeg;
				if (elevationDeg < elMin) elMin = elevationDeg;
				if (elevationDeg > elMax) elMax = elevationDeg;
			}

			_allRays = rays.ToArray();
			_currentIndex = 0;

			AzimuthMinDeg = azMin;
			AzimuthMaxDeg = azMax;
			ElevationMinDeg = elMin;
			ElevationMaxDeg = elMax;

			// Detect full-rotation patterns (e.g. mid360 covers 0°-360°)
			IsFullRotation = (azMax - azMin) > 350f;

			Debug.Log($"[LivoxScanPattern] Loaded {_allRays.Length} rays, " +
					  $"azimuth=[{azMin:F1}°, {azMax:F1}°], " +
					  $"elevation=[{elMin:F1}°, {elMax:F1}°], " +
					  $"fullRotation={IsFullRotation}");
		}

		/// <summary>
		/// Get a window of rays starting from the current index.
		/// Advances the current index by <paramref name="count"/> (the full step,
		/// regardless of downsampling) to produce the non-repetitive rolling pattern.
		/// </summary>
		/// <param name="count">Number of ray slots to step through.</param>
		/// <param name="downSample">Skip factor (1 = no skip, 2 = every other ray).</param>
		/// <param name="output">Pre-allocated array to fill.</param>
		/// <returns>Actual number of rays written to output.</returns>
		public int GetRayWindow(int count, int downSample, LivoxRayInfo[] output)
		{
			if (_allRays == null || _allRays.Length == 0)
				return 0;

			downSample = Mathf.Max(1, downSample);
			var written = 0;
			var total = _allRays.Length;

			for (var i = 0; i < count && written < output.Length; i += downSample)
			{
				var srcIdx = (_currentIndex + i) % total;
				output[written++] = _allRays[srcIdx];
			}

			// Advance rolling window by the full count (not downsampled)
			_currentIndex = (_currentIndex + count) % total;

			return written;
		}
	}
}
