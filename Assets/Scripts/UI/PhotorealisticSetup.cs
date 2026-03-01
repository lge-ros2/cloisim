/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Adds a global URP Volume at runtime with post-processing overrides
/// for high visual quality. Each feature can be toggled individually
/// through the GUI Render Settings panel.
///
/// Replaces the former HDRP PhotorealisticSetup. Some HDRP features
/// have no URP equivalent and are omitted:
///   - Screen-Space Reflections (SSR) — URP has no SSR; use Reflection Probes
///   - Volumetric Fog — URP has no volumetric fog; basic fog via RenderSettings
///   - Indirect Lighting Controller — HDRP-only
///   - Auto Exposure (Histogram) — URP has no automatic exposure
///
/// Available URP post-processing overrides:
///   - Bloom
///   - Tonemapping (ACES)
///   - Color Grading (saturation + contrast + post-exposure)
///   - White Balance
///   - Vignette
/// </summary>
[DefaultExecutionOrder(50)]
public class PhotorealisticSetup : MonoBehaviour
{
	public static PhotorealisticSetup Instance { get; private set; }

	/// <summary>
	/// Identifiers for each toggleable feature.
	/// </summary>
	public enum Feature
	{
		Bloom,
		Tonemapping,
		PostExposure,
		ColorGrading,
		WhiteBalance,
		Vignette,
	}

	private Volume _volume;
	private bool _isActive = true;

	// Per-component references for individual toggling
	private readonly Dictionary<Feature, VolumeComponent> _components = new();
	private readonly Dictionary<Feature, bool> _featureStates = new();

	/// <summary>Whether the master photorealistic volume is active.</summary>
	public bool IsActive => _isActive;

	void Awake()
	{
		Instance = this;

		// All features default to ON
		foreach (Feature f in Enum.GetValues(typeof(Feature)))
		{
			_featureStates[f] = true;
		}
	}

	void Start()
	{
		if (Application.isBatchMode)
		{
			Debug.Log("[PhotorealisticSetup] Skipped in batchmode");
			return;
		}

		SetupPostProcessingVolume();
		SetupBasicFog();
		Debug.Log("[PhotorealisticSetup] URP post-processing volume configured");
	}

	private void SetupPostProcessingVolume()
	{
		var volumeGO = new GameObject("__PhotorealisticVolume__");
		volumeGO.transform.SetParent(transform);

		_volume = volumeGO.AddComponent<Volume>();
		_volume.isGlobal = true;
		_volume.priority = 10f;

		var profile = ScriptableObject.CreateInstance<VolumeProfile>();
		_volume.profile = profile;

		// ── Bloom ──
		var bloom = profile.Add<Bloom>();
		bloom.active = true;
		bloom.threshold.Override(0.8f);
		bloom.intensity.Override(0.3f);
		bloom.scatter.Override(0.65f);
		bloom.highQualityFiltering.Override(true);
		_components[Feature.Bloom] = bloom;

		// ── Tonemapping (ACES) ──
		var tonemap = profile.Add<Tonemapping>();
		tonemap.active = true;
		tonemap.mode.Override(TonemappingMode.ACES);
		_components[Feature.Tonemapping] = tonemap;

		// ── Color Adjustments (post-exposure replaces HDRP's auto-exposure) ──
		var colorAdj = profile.Add<ColorAdjustments>();
		colorAdj.active = true;
		colorAdj.postExposure.Override(0.5f);
		colorAdj.saturation.Override(12f);
		colorAdj.contrast.Override(8f);
		_components[Feature.ColorGrading] = colorAdj;
		_components[Feature.PostExposure] = colorAdj; // shared component

		// ── White Balance ──
		var wb = profile.Add<WhiteBalance>();
		wb.active = true;
		wb.temperature.Override(3f);
		wb.tint.Override(0f);
		_components[Feature.WhiteBalance] = wb;

		// ── Vignette ──
		var vignette = profile.Add<Vignette>();
		vignette.active = true;
		vignette.intensity.Override(0.25f);
		vignette.smoothness.Override(0.4f);
		vignette.rounded.Override(true);
		_components[Feature.Vignette] = vignette;
	}

	/// <summary>
	/// Configure basic fog via RenderSettings as a replacement for HDRP's
	/// volumetric fog (which has no URP equivalent).
	/// </summary>
	private void SetupBasicFog()
	{
		RenderSettings.fog = true;
		RenderSettings.fogMode = FogMode.ExponentialSquared;
		RenderSettings.fogDensity = 0.002f;
		RenderSettings.fogColor = new Color(0.9f, 0.92f, 0.95f);
	}

	/// <summary>
	/// Master toggle — enable or disable the entire post-processing volume.
	/// </summary>
	public void SetEnabled(bool enabled)
	{
		_isActive = enabled;
		if (_volume != null)
		{
			_volume.gameObject.SetActive(enabled);
		}
		Debug.Log($"[PhotorealisticSetup] Post-processing {(enabled ? "enabled" : "disabled")}");
	}

	/// <summary>
	/// Toggle an individual feature on or off.
	/// </summary>
	public void SetFeatureEnabled(Feature feature, bool enabled)
	{
		_featureStates[feature] = enabled;

		if (_components.TryGetValue(feature, out var component))
		{
			component.active = enabled;
		}

		Debug.Log($"[PhotorealisticSetup] {feature} {(enabled ? "enabled" : "disabled")}");
	}

	/// <summary>
	/// Query whether a specific feature is currently enabled.
	/// </summary>
	public bool IsFeatureEnabled(Feature feature)
	{
		return _featureStates.TryGetValue(feature, out var val) && val;
	}

	void OnDestroy()
	{
		if (Instance == this) Instance = null;

		if (_volume != null && _volume.profile != null)
		{
			DestroyImmediate(_volume.profile);
		}
	}
}
