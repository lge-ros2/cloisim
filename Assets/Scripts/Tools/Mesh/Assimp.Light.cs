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
	private static Color ToUnity(this SN.Vector3 color)
		=> new Color(color.X, color.Y, color.Z, 1f);

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

	private static Dictionary<string, Assimp.Light> BuildLightMap(in Assimp.Scene scene)
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
		in Assimp.Light assimpLight,
		in GameObject nodeObject)
	{
		var lightObject = new GameObject(assimpLight.Name);
		lightObject.tag = "Light";

		var lightComponent = lightObject.AddComponent<Light>();
		lightComponent.transform.SetParent(nodeObject.transform);

		lightComponent.renderMode = LightRenderMode.ForcePixel;
		lightComponent.shadows = LightShadows.Hard;
		lightComponent.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;

		// Set diffuse color
		lightComponent.color = assimpLight.ColorDiffuse.ToUnity();

		lightComponent.cullingMask = LayerMask.GetMask("Default", "Plane");

		var attConstant = assimpLight.AttenuationConstant;
		var attLinear = assimpLight.AttenuationLinear;
		var attQuadratic = assimpLight.AttenuationQuadratic;

		var position = assimpLight.Position;
		var direction = assimpLight.Direction;

		switch (assimpLight.LightType)
		{
			case Assimp.LightSourceType.Directional:
				lightComponent.type = LightType.Directional;

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
				lightComponent.intensity = CalculateLightIntensity(attConstant, attLinear, attQuadratic);

				lightComponent.transform.localPosition = new Vector3(position.X, position.Y, position.Z);

				if (direction != SN.Vector3.Zero)
				{
					var unityDir = new Vector3(direction.X, direction.Y, direction.Z);
					lightComponent.transform.localRotation = Quaternion.LookRotation(unityDir);
				}
				break;

			case Assimp.LightSourceType.Area:
				lightComponent.type = LightType.Area;
				var areaSize = assimpLight.AreaSize;
				lightComponent.areaSize = new Vector2(areaSize.X, areaSize.Y);
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);
				lightComponent.intensity = CalculateLightIntensity(attConstant, attLinear, attQuadratic);

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
				lightComponent.intensity = CalculateLightIntensity(attConstant, attLinear, attQuadratic);
				lightComponent.color = assimpLight.ColorAmbient.ToUnity();
				break;

			case Assimp.LightSourceType.Point:
			default:
				lightComponent.type = LightType.Point;
				lightComponent.range = CalculateLightRange(attConstant, attLinear, attQuadratic);
				lightComponent.intensity = CalculateLightIntensity(attConstant, attLinear, attQuadratic);

				lightComponent.transform.localPosition = new Vector3(position.X, position.Y, position.Z);
				break;
		}

		// Debug.Log($"Light created: {assimpLight.Name}, Type: {assimpLight.LightType}, Color: {lightComponent.color}");
	}
}
