/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Centralized URP render quality settings shared between the GUI camera
/// (Camera.main) and all RGB sensor cameras.
///
/// In GUI mode (interactive), full URP quality is used by default.
/// In headless/batchmode, expensive features are disabled for performance.
/// The Settings GUI toggles update this manager, which propagates to all cameras.
///
/// Replaces the former HDRP FrameSettingsField-based manager. URP uses
/// UniversalAdditionalCameraData properties for per-camera quality control.
/// </summary>
public static class RenderQualityManager
{
	/// <summary>
	/// Quality features that can be toggled per-camera in URP.
	/// Maps conceptually to HDRP FrameSettingsField but uses URP APIs.
	/// </summary>
	public enum QualityFeature
	{
		Shadows,
		PostProcessing,
		// SSAO is controlled via renderer feature, not per-camera
		// SSR, Volumetrics, SubsurfaceScattering — not available in URP
	}

	// Current override state: feature → enabled.
	private static readonly Dictionary<QualityFeature, bool> _overrides = new();

	private static bool _initialized = false;
	private static bool _sensorOnlyMode = false;

	/// <summary>
	/// Set sensor-only mode flag before Initialize() is called.
	/// When true, the GUI camera uses stripped features (matching sensor cameras).
	/// </summary>
	public static void SetSensorOnlyMode(bool enabled)
	{
		_sensorOnlyMode = enabled;
	}

	/// <summary>
	/// Ensures default overrides are populated. Safe to call multiple times.
	/// In GUI mode: no overrides — full URP quality (all features enabled).
	/// In sensor-only mode: expensive features disabled (matches sensor cameras).
	/// In batchmode: expensive features disabled for performance.
	/// </summary>
	public static void Initialize()
	{
		if (_initialized) return;
		_initialized = true;

		if (Application.isBatchMode)
		{
			_overrides[QualityFeature.Shadows] = false;
			_overrides[QualityFeature.PostProcessing] = false;
			Debug.Log("[RenderQualityManager] Headless mode: disabled expensive URP features");
		}
		else if (_sensorOnlyMode)
		{
			_overrides[QualityFeature.Shadows] = false;
			_overrides[QualityFeature.PostProcessing] = false;
			Debug.Log("[RenderQualityManager] Sensor-only mode: disabled expensive URP features for GUI camera");
		}
		else
		{
			Debug.Log("[RenderQualityManager] GUI mode: using full URP rendering quality");
		}
	}

	/// <summary>
	/// Query whether a feature is enabled. Returns null if not overridden.
	/// </summary>
	public static bool? IsEnabled(QualityFeature feature)
	{
		Initialize();
		return _overrides.TryGetValue(feature, out var val) ? val : null;
	}

	/// <summary>
	/// Set a feature override and propagate to all cameras (GUI + sensors).
	/// </summary>
	public static void SetFeature(QualityFeature feature, bool enabled)
	{
		Initialize();
		_overrides[feature] = enabled;
		ApplyToAllCameras();
	}

	/// <summary>
	/// Apply all current overrides to a single UniversalAdditionalCameraData.
	/// Called by sensor Camera.cs when it initializes its URP camera data.
	/// </summary>
	public static void ApplyTo(UniversalAdditionalCameraData urpCamData)
	{
		if (urpCamData == null) return;
		Initialize();

		if (_overrides.Count == 0)
		{
			// No overrides — use native URP pipeline defaults (full quality).
			return;
		}

		foreach (var kvp in _overrides)
		{
			switch (kvp.Key)
			{
				case QualityFeature.Shadows:
					urpCamData.renderShadows = kvp.Value;
					break;
				case QualityFeature.PostProcessing:
					urpCamData.renderPostProcessing = kvp.Value;
					break;
			}
		}
	}

	/// <summary>
	/// Apply current overrides to Camera.main and all SensorDevices.Camera instances.
	/// </summary>
	public static void ApplyToAllCameras()
	{
		// GUI camera
		var mainCam = Camera.main;
		if (mainCam != null)
		{
			var urpData = mainCam.GetComponent<UniversalAdditionalCameraData>();
			ApplyTo(urpData);
		}

		// All sensor cameras (RGB, Depth, Segmentation)
		var sensorCameras = Object.FindObjectsByType<SensorDevices.Camera>(FindObjectsSortMode.None);
		foreach (var sensorCam in sensorCameras)
		{
			if (sensorCam.UniversalCameraData != null)
			{
				ApplyTo(sensorCam.UniversalCameraData);
			}
		}
	}

	/// <summary>
	/// Returns all current overrides for UI initialization.
	/// </summary>
	public static IReadOnlyDictionary<QualityFeature, bool> GetOverrides()
	{
		Initialize();
		return _overrides;
	}
}
