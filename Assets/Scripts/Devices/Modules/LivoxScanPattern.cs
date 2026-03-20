/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Globalization;
using System.IO;
using UnityEngine;

namespace SensorDevices
{
	/// <summary>
	/// Loads and manages a Livox-style non-repetitive scan pattern from a CSV file.
	/// CSV format: Time/s,Azimuth/deg,Zenith/deg
	///
	/// Azimuth: horizontal rotation angle (0–360 degrees)
	/// Zenith: polar angle from Z-up (standard spherical coordinates)
	///   - 0° = straight up, 90° = horizontal, >90° = below horizontal
	///
	/// Each update cycle, a subset of rays (samplesPerCycle) is cast starting from
	/// a rotating index into the pattern, creating the non-repetitive scan effect.
	/// </summary>
	public class LivoxScanPattern
	{
		/// <summary>All pattern entries: (azimuth_rad, zenith_rad) pairs.</summary>
		private Vector2[] _pattern;

		private int _patternSize;
		private int _samplesPerCycle;
		private int _downsample;
		private int _currentStartIndex;

		public int PatternSize => _patternSize;
		public int SamplesPerCycle => _samplesPerCycle;
		public int Downsample => _downsample;
		public int TotalRaysPerCycle => _samplesPerCycle / _downsample;
		public Vector2[] Pattern => _pattern;
		public int CurrentStartIndex => _currentStartIndex;

		/// <summary>
		/// Load a Livox scan pattern from a CSV file.
		/// </summary>
		/// <param name="csvPath">Absolute path to the CSV file.</param>
		/// <param name="samplesPerCycle">Number of pattern entries to step through per cycle.</param>
		/// <param name="downsample">Step size through pattern entries (1 = every entry).</param>
		/// <returns>True if loaded successfully.</returns>
		public bool Load(string csvPath, int samplesPerCycle, int downsample)
		{
			if (!File.Exists(csvPath))
			{
				Debug.LogError($"[LivoxScanPattern] CSV file not found: {csvPath}");
				return false;
			}

			_samplesPerCycle = samplesPerCycle;
			_downsample = Mathf.Max(1, downsample);
			_currentStartIndex = 0;

			var lines = File.ReadAllLines(csvPath);

			// Detect and skip header line (non-numeric first character)
			var dataStart = 0;
			if (lines.Length > 0)
			{
				var firstChar = lines[0].TrimStart()[0];
				if (!char.IsDigit(firstChar) && firstChar != '-' && firstChar != '+')
				{
					dataStart = 1;
				}
			}

			_patternSize = lines.Length - dataStart;
			_pattern = new Vector2[_patternSize];

			const float deg2Rad = Mathf.Deg2Rad;
			var validCount = 0;

			for (var i = dataStart; i < lines.Length; i++)
			{
				var parts = lines[i].Split(',');
				if (parts.Length >= 3)
				{
					var azimuth = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture) * deg2Rad;
					var zenith = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture) * deg2Rad;
					_pattern[validCount] = new Vector2(azimuth, zenith);
					validCount++;
				}
			}

			if (validCount != _patternSize)
			{
				var trimmed = new Vector2[validCount];
				System.Array.Copy(_pattern, trimmed, validCount);
				_pattern = trimmed;
				_patternSize = validCount;
			}

			Debug.Log($"[LivoxScanPattern] Loaded {_patternSize} entries from {Path.GetFileName(csvPath)}, " +
				$"samplesPerCycle={_samplesPerCycle}, downsample={_downsample}, " +
				$"raysPerCycle={TotalRaysPerCycle}");

			return _patternSize > 0;
		}

		/// <summary>Advance the start index by one cycle for the next frame.</summary>
		public void AdvanceCycle()
		{
			_currentStartIndex = (_currentStartIndex + _samplesPerCycle) % _patternSize;
		}

		/// <summary>Reset to the beginning of the pattern.</summary>
		public void Reset()
		{
			_currentStartIndex = 0;
		}
	}
}
