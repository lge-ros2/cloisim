/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace SensorDevices
{
	public partial class Lidar
	{
		[SerializeField] private static int _indexForVisualize = 0;
		[SerializeField] private static int _maxCountForVisualize = 3;
		[SerializeField] private static float _hueOffsetForVisualize = 0f;
		[SerializeField] private const float UnitHueOffsetForVisualize = 0.07f;
		[SerializeField] private const float AlphaForVisualize = 0.75f;

		/// <summary>
		/// 3D lidar: renders hit positions as a point cloud using ParticleSystem.
		/// Each point is colored by vertical angle (elevation) for visual clarity.
		/// </summary>
		private IEnumerator OnVisualizePointCloud(GameObject visualizer)
		{
			var ps = visualizer?.AddComponent<ParticleSystem>();
			if (ps == null)
				yield break;

			var main = ps.main;
			main.loop = false;
			main.playOnAwake = false;
			main.maxParticles = (int)_totalSamples;
			main.startLifetime = float.MaxValue;
			main.startSpeed = 0f;
			main.startSize = 0.004f;
			main.simulationSpace = ParticleSystemSimulationSpace.World;

			var emission = ps.emission;
			emission.enabled = false;

			var shape = ps.shape;
			shape.enabled = false;

			var psRenderer = visualizer.GetComponent<ParticleSystemRenderer>();
			psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
			var particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
			particleMat.SetTexture("_BaseMap", Resources.Load<Texture2D>("Default-Particle"));
			particleMat.hideFlags = HideFlags.DontUnloadUnusedAsset;
			psRenderer.material = particleMat;

			ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			if (_indexForVisualize >= _maxCountForVisualize)
			{
				_indexForVisualize = 0;
				_hueOffsetForVisualize += UnitHueOffsetForVisualize;
			}
			var hue = ((float)_indexForVisualize++ / Mathf.Max(1, _maxCountForVisualize)) + _hueOffsetForVisualize;
			hue = (hue % 1f + 1f) % 1f;

			var particles = new ParticleSystem.Particle[_totalSamples];

			if (IsLivoxMode)
			{
				yield return OnVisualizePointCloudLivox(ps, particles, waitForSeconds, hue);
			}
			else
			{
				yield return OnVisualizePointCloudStandard(ps, particles, waitForSeconds, hue);
			}
		}

		/// <summary>
		/// Livox point cloud visualization: reads pre-computed XYZ triples from Ranges.
		/// </summary>
		private IEnumerator OnVisualizePointCloudLivox(
			ParticleSystem ps, ParticleSystem.Particle[] particles,
			WaitForSeconds waitForSeconds, float hue)
		{
			var rangeMax = _scanRange.max;

			while (true)
			{
				var rangeData = GetRangeData();
				if (rangeData == null)
				{
					yield return waitForSeconds;
					continue;
				}

				var particleCount = 0;
				var sensorWorldRotation = transform.rotation;
				var sensorWorldPosition = transform.position;

				// Ranges stores XYZ triples: [x0, y0, z0, x1, y1, z1, ...]
				var pointCount = (int)_totalSamples;
				for (var i = 0; i < pointCount; i++)
				{
					var baseIdx = i * XYZComponents;
					if (baseIdx + 2 >= rangeData.Count)
						break;

					var x = (float)rangeData[baseIdx + 0];
					var y = (float)rangeData[baseIdx + 1];
					var z = (float)rangeData[baseIdx + 2];

					if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z))
						continue;

					// Convert from sensor-local SDF frame (X-fwd, Y-left, Z-up)
					// to Unity world space
					var localPos = SDF2Unity.Position(x, y, z);
					var worldPos = sensorWorldPosition + sensorWorldRotation * localPos;

					// Color by elevation (z component in SDF frame)
					// Prevent index out of bounds if lidar parameters change at runtime
					if (particleCount >= particles.Length)
						break;

					var elevation = Mathf.Atan2(z, Mathf.Sqrt(x * x + y * y));
					var t = Mathf.InverseLerp(-0.15f, 1.0f, elevation);
					var pointColor = Color.HSVToRGB((hue + t) % 1.0f, 0.9f, 0.95f);
					pointColor.a = AlphaForVisualize;

					particles[particleCount].position = worldPos;
					particles[particleCount].startColor = pointColor;
					particles[particleCount].startSize = 0.004f;
					particles[particleCount].remainingLifetime = 1f;
					particleCount++;
				}

				ps.SetParticles(particles, particleCount);

				yield return waitForSeconds;
			}
		}

		/// <summary>
		/// Standard 3D lidar point cloud visualization: computes positions from angles + ranges.
		/// </summary>
		private IEnumerator OnVisualizePointCloudStandard(
			ParticleSystem ps, ParticleSystem.Particle[] particles,
			WaitForSeconds waitForSeconds, float hue)
		{
			var startAngleH = _horizontal.angle.min;
			var startAngleV = _vertical.angle.min;
			var endAngleV = _vertical.angle.max;
			var horizontalSamples = _horizontal.samples;
			var rangeMin = _scanRange.min;
			var rangeMax = _scanRange.max;

			while (true)
			{
				var rangeData = GetRangeData();
				if (rangeData == null)
				{
					yield return waitForSeconds;
					continue;
				}

				var particleCount = 0;
				var rayStartBase = transform.position;
				var sensorWorldRotation = transform.rotation;

				for (var scanIndex = 0; scanIndex < rangeData.Count; scanIndex++)
				{
					var scanIndexH = scanIndex % horizontalSamples;
					var scanIndexV = scanIndex / horizontalSamples;

					var rayAngleH = startAngleH + (_resolution.angleH * scanIndexH);
					var rayAngleV = startAngleV + (_resolution.angleV * scanIndexV);

					var rayData = (float)rangeData[scanIndex];

					if (float.IsNaN(rayData) || rayData > rangeMax)
						continue;

					// Prevent index out of bounds if lidar parameters change at runtime
					if (particleCount >= particles.Length)
						break;

					var localAngles = Quaternion.AngleAxis(-rayAngleH, Vector3.up) * Quaternion.AngleAxis(rayAngleV, -Vector3.right);
					var dir = sensorWorldRotation * localAngles * Vector3.forward;
					dir.Normalize();

					var hitPos = rayStartBase + dir * rayData;

					var t = Mathf.InverseLerp(startAngleV, endAngleV, rayAngleV);
					var pointColor = Color.HSVToRGB((hue + t) % 1.0f, 0.9f, 0.95f);
					pointColor.a = AlphaForVisualize;

					particles[particleCount].position = hitPos;
					particles[particleCount].startColor = pointColor;
					particles[particleCount].startSize = 0.004f;
					particles[particleCount].remainingLifetime = 1f;
					particleCount++;
				}

				ps.SetParticles(particles, particleCount);

				yield return waitForSeconds;
			}
		}

		/// <summary>
		/// 2D lidar: renders rays as line segments using LineRenderer.
		/// </summary>
		private IEnumerator OnVisualizeLines(GameObject visualizer)
		{
			var lineRenderer = visualizer?.AddComponent<LineRenderer>();
			if (lineRenderer == null)
				yield break;

			lineRenderer.positionCount = 0;
			lineRenderer.widthMultiplier = 0.001f;
			lineRenderer.material = new Material(Shader.Find("Sprites/Default"))
			{
				hideFlags = HideFlags.DontUnloadUnusedAsset
			};
			lineRenderer.useWorldSpace = true;

			var waitForSeconds = new WaitForSeconds(UpdatePeriod);
			var startAngleH = _horizontal.angle.min;
			var startAngleV = _vertical.angle.min;
			var endAngleV = _vertical.angle.max;
			var horizontalSamples = _horizontal.samples;
			var rangeMin = _scanRange.min;
			var rangeMax = _scanRange.max;

			if (_indexForVisualize >= _maxCountForVisualize)
			{
				_indexForVisualize = 0;
				_hueOffsetForVisualize += UnitHueOffsetForVisualize;
			}
			var hue = ((float)_indexForVisualize++ / Mathf.Max(1, _maxCountForVisualize)) + _hueOffsetForVisualize;
			hue = (hue % 1f + 1f) % 1f;

			var positions = new List<Vector3>((int)(horizontalSamples * _vertical.samples) * 2);

			while (true)
			{
				var rangeData = GetRangeData();
				if (rangeData == null)
				{
					yield return waitForSeconds;
					continue;
				}

				positions.Clear();
				var rayStartBase = transform.position;
				var sensorWorldRotation = transform.rotation;

				for (var scanIndex = 0; scanIndex < rangeData.Count; scanIndex++)
				{
					var scanIndexH = scanIndex % horizontalSamples;
					var scanIndexV = scanIndex / horizontalSamples;

					var rayAngleH = startAngleH + (_resolution.angleH * scanIndexH);
					var rayAngleV = startAngleV + (_resolution.angleV * scanIndexV);

					var rayData = (float)rangeData[scanIndex];

					if (float.IsNaN(rayData) || rayData > rangeMax)
						continue;

					var localAngles = Quaternion.AngleAxis(-rayAngleH, Vector3.up) * Quaternion.AngleAxis(rayAngleV, -Vector3.right);
					var dir = sensorWorldRotation * localAngles * Vector3.forward;
					dir.Normalize();

					var start = rayStartBase + dir * rangeMin;
					var end = start + dir * (rayData - rangeMin);

					positions.Add(start);
					positions.Add(end);
				}

				lineRenderer.positionCount = positions.Count;
				lineRenderer.SetPositions(positions.ToArray());

				var baseColor = Color.HSVToRGB(hue, 0.9f, 1f);
				baseColor.a = AlphaForVisualize;
				lineRenderer.startColor = baseColor;
				lineRenderer.endColor = baseColor;

				yield return waitForSeconds;
			}
		}
	}
}
