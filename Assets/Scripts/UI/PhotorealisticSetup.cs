/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Adds a global HDRP Volume at runtime with photorealistic post-processing
/// overrides for maximum visual quality. Each feature can be toggled
/// individually through the GUI Render Settings panel.
///
/// Post-Processing features (override DefaultSettingsVolumeProfile):
///   - Bloom
///   - Tonemapping (Full ACES)
///   - Auto Exposure (Histogram)
///
/// Photorealistic features (runtime volume):
///   - Screen-Space Reflections (SSR) at high quality
///   - Volumetric Fog
///   - Color Grading (saturation + contrast)
///   - White Balance
///   - Vignette
///   - Indirect Lighting boost
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
		// ── Post-Processing (DefaultSettingsVolumeProfile overrides) ──
		Bloom,
		Tonemapping,
		AutoExposure,

		// ── Photorealistic (runtime volume) ──
		SSR,
		VolumetricFog,
		ColorGrading,
		WhiteBalance,
		Vignette,
		IndirectLighting,
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

		SetupPhotorealisticVolume();
		Debug.Log("[PhotorealisticSetup] Photorealistic HDRP volume configured");
	}

	private void SetupPhotorealisticVolume()
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
		bloom.quality.Override((int)ScalableSettingLevelParameter.Level.High);
		bloom.threshold.Override(0.8f);
		bloom.intensity.Override(0.3f);
		bloom.scatter.Override(0.65f);
		bloom.highQualityPrefiltering = true;
		_components[Feature.Bloom] = bloom;

		// ── Tonemapping (Full ACES) ──
		var tonemap = profile.Add<Tonemapping>();
		tonemap.active = true;
		tonemap.mode.Override(TonemappingMode.ACES);
		tonemap.useFullACES.value = true;
		_components[Feature.Tonemapping] = tonemap;

		// ── Auto Exposure (Histogram) ──
		var exposure = profile.Add<Exposure>();
		exposure.active = true;
		exposure.mode.Override(ExposureMode.AutomaticHistogram);
		exposure.limitMin.Override(-2f);
		exposure.limitMax.Override(16f);
		_components[Feature.AutoExposure] = exposure;

		// ── Screen-Space Reflections (SSR) ──
		var ssr = profile.Add<ScreenSpaceReflection>();
		ssr.enabled.Override(true);
		ssr.quality.Override((int)ScalableSettingLevelParameter.Level.High);
		_components[Feature.SSR] = ssr;

		// ── Volumetric Fog ──
		var fog = profile.Add<Fog>();
		fog.enabled.Override(true);
		fog.meanFreePath.Override(50f);
		fog.baseHeight.Override(0f);
		fog.maximumHeight.Override(10f);
		fog.enableVolumetricFog.Override(true);
		fog.albedo.Override(new Color(0.9f, 0.92f, 0.95f));
		fog.globalLightProbeDimmer.Override(1.0f);
		_components[Feature.VolumetricFog] = fog;

		// ── Color Grading (saturation + contrast) ──
		var colorAdj = profile.Add<ColorAdjustments>();
		colorAdj.saturation.Override(12f);
		colorAdj.contrast.Override(8f);
		colorAdj.postExposure.Override(0.0f);
		_components[Feature.ColorGrading] = colorAdj;

		// ── White Balance ──
		var wb = profile.Add<WhiteBalance>();
		wb.temperature.Override(3f);
		wb.tint.Override(0f);
		_components[Feature.WhiteBalance] = wb;

		// ── Vignette ──
		var vignette = profile.Add<Vignette>();
		vignette.intensity.Override(0.25f);
		vignette.smoothness.Override(0.4f);
		vignette.rounded.Override(true);
		_components[Feature.Vignette] = vignette;

		// ── Indirect Lighting Controller ──
		var ilc = profile.Add<IndirectLightingController>();
		ilc.indirectDiffuseLightingMultiplier.Override(1.2f);
		ilc.reflectionLightingMultiplier.Override(1.1f);
		_components[Feature.IndirectLighting] = ilc;
	}

	/// <summary>
	/// Master toggle — enable or disable the entire photorealistic volume.
	/// </summary>
	public void SetEnabled(bool enabled)
	{
		_isActive = enabled;
		if (_volume != null)
		{
			_volume.gameObject.SetActive(enabled);
		}
		Debug.Log($"[PhotorealisticSetup] Photorealistic mode {(enabled ? "enabled" : "disabled")}");
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
