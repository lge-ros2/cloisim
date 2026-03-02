/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Centralized HDRP render quality settings that are shared between
/// the GUI camera (Camera.main) and all RGB sensor cameras.
///
/// In GUI mode (interactive), full HDRP quality is used by default so the
/// viewport and RGB sensor images match the native HDRP rendering.
/// In headless/batchmode, expensive features are disabled for performance.
/// The Settings GUI toggles update this manager, which propagates to all cameras.
/// </summary>
public static class RenderQualityManager
{
	// Expensive HDRP features that are disabled in headless mode for performance.
	// In GUI mode these are left at HDRP defaults (enabled).
	private static readonly HashSet<FrameSettingsField> HeadlessDisabledFeatures = new()
	{
		FrameSettingsField.SSR,
		FrameSettingsField.Volumetrics,
		FrameSettingsField.ContactShadows,
		FrameSettingsField.ScreenSpaceShadows,
		FrameSettingsField.SubsurfaceScattering,
		FrameSettingsField.Refraction,
		FrameSettingsField.MotionVectors,
	};

	// Additional features disabled in sensor-only mode (GUI camera doesn't
	// need photorealistic output when only sensors matter).
	private static readonly HashSet<FrameSettingsField> SensorOnlyDisabledFeatures = new()
	{
		FrameSettingsField.Postprocess,
		FrameSettingsField.ShadowMaps,
		FrameSettingsField.SSAO,
		FrameSettingsField.SSR,
		FrameSettingsField.Volumetrics,
		FrameSettingsField.ReprojectionForVolumetrics,
		FrameSettingsField.AtmosphericScattering,
		FrameSettingsField.SubsurfaceScattering,
		FrameSettingsField.ContactShadows,
		FrameSettingsField.ScreenSpaceShadows,
		FrameSettingsField.MotionVectors,
		FrameSettingsField.Decals,
		FrameSettingsField.Refraction,
	};

	// Current override state: field → enabled.
	// Only fields present here are overridden; others use HDRP defaults.
	private static readonly Dictionary<FrameSettingsField, bool> _overrides = new();

	private static bool _initialized = false;
	private static bool _sensorOnlyMode = false;

	/// <summary>
	/// Set sensor-only mode flag before Initialize() is called.
	/// When true, the GUI camera uses stripped HDRP features (matching sensor cameras).
	/// </summary>
	public static void SetSensorOnlyMode(bool enabled)
	{
		_sensorOnlyMode = enabled;
	}

	/// <summary>
	/// Ensures default overrides are populated. Safe to call multiple times.
	/// In GUI mode: no overrides → full HDRP quality (all features enabled).
	/// In sensor-only mode: expensive features disabled (matches sensor cameras).
	/// In batchmode: expensive features disabled for performance.
	/// </summary>
	public static void Initialize()
	{
		if (_initialized) return;
		_initialized = true;

		if (Application.isBatchMode)
		{
			foreach (var field in HeadlessDisabledFeatures)
			{
				_overrides[field] = false;
			}
			Debug.Log("[RenderQualityManager] Headless mode: disabled expensive HDRP features");
		}
		else if (_sensorOnlyMode)
		{
			foreach (var field in SensorOnlyDisabledFeatures)
			{
				_overrides[field] = false;
			}
			Debug.Log("[RenderQualityManager] Sensor-only mode: disabled expensive HDRP features for GUI camera");
		}
		else
		{
			Debug.Log("[RenderQualityManager] GUI mode: using full HDRP rendering quality");
		}
	}

	/// <summary>
	/// Query whether a feature is enabled. Returns null if not overridden.
	/// </summary>
	public static bool? IsEnabled(FrameSettingsField field)
	{
		Initialize();
		return _overrides.TryGetValue(field, out var val) ? val : null;
	}

	/// <summary>
	/// Set a feature override and propagate to all cameras (GUI + sensors).
	/// </summary>
	public static void SetFeature(FrameSettingsField field, bool enabled)
	{
		Initialize();
		_overrides[field] = enabled;
		ApplyToAllCameras();
	}

	/// <summary>
	/// Apply all current overrides to a single HDAdditionalCameraData.
	/// Called by sensor Camera.cs when it initializes its HDRP camera data.
	/// </summary>
	public static void ApplyTo(HDAdditionalCameraData hdCamData)
	{
		if (hdCamData == null) return;
		Initialize();

		if (_overrides.Count == 0)
		{
			// No overrides — use native HDRP pipeline defaults (full quality).
			hdCamData.customRenderingSettings = false;
			return;
		}

		hdCamData.customRenderingSettings = true;
		var overrideMask = hdCamData.renderingPathCustomFrameSettingsOverrideMask;
		var frameSettings = hdCamData.renderingPathCustomFrameSettings;

		foreach (var kvp in _overrides)
		{
			overrideMask.mask[(uint)kvp.Key] = true;
			frameSettings.SetEnabled(kvp.Key, kvp.Value);
		}

		hdCamData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;
		hdCamData.renderingPathCustomFrameSettings = frameSettings;
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
			var hdData = mainCam.GetComponent<HDAdditionalCameraData>();
			ApplyTo(hdData);
		}

		// All sensor cameras (RGB, Depth, Segmentation — any that use HDRP pipeline)
		var sensorCameras = Object.FindObjectsByType<SensorDevices.Camera>(FindObjectsSortMode.None);
		foreach (var sensorCam in sensorCameras)
		{
			if (sensorCam.HdCameraData != null)
			{
				ApplyTo(sensorCam.HdCameraData);
			}
		}
	}

	/// <summary>
	/// Returns all current overrides for UI initialization.
	/// </summary>
	public static IReadOnlyDictionary<FrameSettingsField, bool> GetOverrides()
	{
		Initialize();
		return _overrides;
	}
}
