/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using SN = System.Numerics;

public static partial class MeshLoader
{
	// Gain factor for converting Blender's physical light units (Watts) to Unity URP intensity.
	// Blender computes point light irradiance as E = P / (4π × d²),
	// so 4π (≈12.57) serves as a physically-motivated base conversion factor.
	// Adjust this value if lights still appear too dark or too bright.
	private static readonly float LightIntensityGain = 4f * Mathf.PI;

	/// <summary>
	/// Separates an HDR color (where intensity may be baked into RGB channels) into
	/// a normalized [0,1] color and a scalar intensity value.
	/// </summary>
	private static void DecomposeHDRColor(
		this SN.Vector3 hdrColor,
		out Color normalizedColor,
		out float intensity)
	{
		var maxComponent = Mathf.Max(hdrColor.X, Mathf.Max(hdrColor.Y, hdrColor.Z));

		if (maxComponent > 0f)
		{
			normalizedColor = new Color(
				hdrColor.X / maxComponent,
				hdrColor.Y / maxComponent,
				hdrColor.Z / maxComponent,
				1f);
			intensity = maxComponent;
		}
		else
		{
			normalizedColor = Color.white;
			intensity = 0f;
		}
	}

	private static float CalculateLightIntensity(
		in float attenuationConstant,
		in float attenuationLinear,
		in float attenuationQuadratic)
	{
		var attenuationFactor = 1.0f / Mathf.Max(0.001f, attenuationConstant + attenuationLinear + attenuationQuadratic);
		return Mathf.Clamp(attenuationFactor, 0.1f, 10f);
	}

	private static float CalculateLightRange(
		in float attenuationConstant,
		in float attenuationLinear,
		in float attenuationQuadratic)
	{
		// Estimate range where attenuation reduces intensity to ~1%
		// Atten = 1 / (att0 + att1 * d + att2 * d*d)
		// Solve for d when Atten = 0.01 => att0 + att1*d + att2*d*d = 100
		if (attenuationQuadratic > 0.0001f)
		{
			var discriminant = attenuationLinear * attenuationLinear - 4f * attenuationQuadratic * (attenuationConstant - 100f);
			if (discriminant >= 0)
			{
				return (-attenuationLinear + Mathf.Sqrt(discriminant)) / (2f * attenuationQuadratic);
			}
		}
		else if (attenuationLinear > 0.0001f)
		{
			return (100f - attenuationConstant) / attenuationLinear;
		}

		return 100f; // default range
	}

	private static Dictionary<string, Assimp.Light> BuildLightMap(this Assimp.Scene scene)
	{
		var lightMap = new Dictionary<string, Assimp.Light>();

		if (scene.HasLights)
		{
			foreach (var light in scene.Lights)
			{
				if (!string.IsNullOrEmpty(light.Name))
				{
					lightMap[light.Name] = light;
				}
			}
		}

		return lightMap;
	}

	private static void ApplyLightToNode(
		this Assimp.Light assimpLight,
		in GameObject nodeObject)
	{
		var lightObject = new GameObject(assimpLight.Name);
		lightObject.tag = "Light";

		var lightComponent = lightObject.AddComponent<Light>();
		lightComponent.transform.SetParent(nodeObject.transform);

		lightComponent.renderMode = LightRenderMode.Auto;

		// Disable shadows for point/spot/area lights — very expensive (cubemap renders)
		// Only directional lights get shadows
		lightComponent.shadows = LightShadows.None;
		lightComponent.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;

		// Decompose HDR color: Blender bakes light power into the color channels,
		// so (R,G,B) can be > 1.0. Separate into normalized color + scalar intensity.
		assimpLight.ColorDiffuse.DecomposeHDRColor(out var lightColor, out var colorIntensity);
		lightComponent.color = lightColor;

		lightComponent.cullingMask = LayerMask.GetMask("Default", "Plane");

		var attConstant = assimpLight.AttenuationConstant;
		var attLinear = assimpLight.AttenuationLinear;
		var attQuadratic = assimpLight.AttenuationQuadratic;

		var position = assimpLight.Position;
		var direction = assimpLight.Direction;

		// Apply gain-adjusted intensity: colorIntensity (from HDR color) × gain factor
		var baseIntensity = colorIntensity * LightIntensityGain;

		switch (assimpLight.LightType)
		{
			case Assimp.LightSourceType.Directional:
				lightComponent.type = LightType.Directional;
				lightComponent.intensity = baseIntensity;
				lightComponent.shadows = LightShadows.Hard; // Only directional lights get shadows
				lightComponent.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;

				if (direction != SN.Vector3.Zero)
				{
					var unityDir = new Vector3(direction.X, direction.Y, direction.Z);
					lightComponent.transform.localRotation = Quaternion.LookRotation(Vector3.down, unityDir);
				}
				break;

			case Assimp.LightSourceType.Spot:
				lightComponent.type = LightType.Spot;
				lightComponent.spotAngle = assimpLight.AngleOuterCone * Mathf.Rad2Deg;
				lightComponent.innerSpotAngle = assimpLight.AngleInnerCone * Mathf.Rad2Deg;
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);
				lightComponent.intensity = baseIntensity;

				lightComponent.transform.localPosition = new Vector3(position.X, position.Y, position.Z);

				if (direction != SN.Vector3.Zero)
				{
					var unityDir = new Vector3(direction.X, direction.Y, direction.Z);
					lightComponent.transform.localRotation = Quaternion.LookRotation(unityDir);
				}
				break;

			case Assimp.LightSourceType.Area:
				lightComponent.type = LightType.Area;
#if UNITY_EDITOR
				var areaSize = assimpLight.AreaSize;
				lightComponent.areaSize = new Vector2(areaSize.X, areaSize.Y);
#endif
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);
				lightComponent.intensity = baseIntensity;

				lightComponent.transform.localPosition = new Vector3(position.X, position.Y, position.Z);

				if (direction != SN.Vector3.Zero)
				{
					var unityDir = new Vector3(direction.X, direction.Y, direction.Z);
					lightComponent.transform.localRotation = Quaternion.LookRotation(unityDir);
				}
				break;

			case Assimp.LightSourceType.Ambient:
				lightComponent.type = LightType.Point;
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);

				// For ambient, use ambient color instead of diffuse
				DecomposeHDRColor(assimpLight.ColorAmbient, out var ambientColor, out var ambientIntensity);
				lightComponent.color = ambientColor;
				lightComponent.intensity = ambientIntensity * LightIntensityGain;
				break;

			case Assimp.LightSourceType.Point:
			default:
				lightComponent.type = LightType.Point;
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);
				lightComponent.intensity = baseIntensity;

				lightComponent.transform.localPosition = new Vector3(position.X, position.Y, position.Z);
				break;
		}

#if UNITY_EDITOR
		Debug.Log($"Light created: {assimpLight.Name}, Type: {assimpLight.LightType}, Color: {lightComponent.color}, Intensity: {lightComponent.intensity} (raw HDR: {colorIntensity}, gain: {LightIntensityGain})");
#endif
	}
}
