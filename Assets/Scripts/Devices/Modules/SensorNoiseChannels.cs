/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

namespace SensorDevices
{
	/// <summary>
	/// Holds one Noise instance per named channel and applies it to individual
	/// scalar/vector components, replacing the duplicated noise-holder classes and
	/// repeated per-axis null checks found in GPS/IMU. Channels are addressed by
	/// name (e.g. "horizontal"/"vertical" for GPS, "x"/"y"/"z" for IMU).
	/// </summary>
	public class SensorNoiseChannels
	{
		private readonly Dictionary<string, Noise> _channels;

		public SensorNoiseChannels(in string[] channelKeys, in Noise defaultNoise = null)
		{
			_channels = new Dictionary<string, Noise>(channelKeys.Length);
			foreach (var key in channelKeys)
			{
				_channels[key] = defaultNoise;
			}
		}

		public Noise this[in string key]
		{
			get => _channels.TryGetValue(key, out var noise) ? noise : null;
			set => _channels[key] = value;
		}

		public void Apply(in string key, ref double value, in float deltaTime = 0)
		{
			if (_channels.TryGetValue(key, out var noise) && noise != null)
			{
				noise.Apply(ref value, deltaTime);
			}
		}

		public void Apply(in string key, ref float value, in float deltaTime = 0)
		{
			if (_channels.TryGetValue(key, out var noise) && noise != null)
			{
				noise.Apply(ref value, deltaTime);
			}
		}
	}
}
