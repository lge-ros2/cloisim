/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Mathf = UnityEngine.Mathf;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			static private float GetIntensity(in Light light)
			{
				var range = (float)light.AttenuationRange;
				var constant = (float)light.ConstantAttenuationFactor;
				var linear = (float)light.LinearAttenuationFactor;
				var quadratic = (float)light.QuadraticAttenuationFactor;
				var attenuationFactor = 1.0f / Mathf.Max(0.001f, constant + linear + quadratic);
				return Mathf.Clamp(range * attenuationFactor, 0.1f, 10f);
			}

			protected override void ImportLight(in Light light, in System.Object parentObject)
			{
				if (light == null)
				{
					return;
				}

				var targetObject = parentObject as UE.GameObject;
				var newLightObject = new UE.GameObject(light.Name)
				{
					tag = "Light"
				};

				var lightComponent = newLightObject.AddComponent<UE.Light>();

				lightComponent.transform.SetParent(targetObject.transform);

				lightComponent.renderMode = UE.LightRenderMode.Auto;

				var lightTypeStr = light.TypeString();

				if (light.Type == LightType.Directional)
				{
					lightComponent.shadows = light.CastShadows ? UE.LightShadows.Hard : UE.LightShadows.None;
					lightComponent.shadowResolution = UE.Rendering.LightShadowResolution.Medium;
				}
				else
				{
					lightComponent.shadows = UE.LightShadows.None;
					lightComponent.shadowResolution = UE.Rendering.LightShadowResolution.Low;
					lightComponent.renderMode = UE.LightRenderMode.ForcePixel;
				}

				lightComponent.color = light.Diffuse.ToUnity();
				lightComponent.cullingMask = UE.LayerMask.GetMask("Default", "Plane");

				var direction = light.Direction.ToUnity();

				var defaultLightDirection = UE.Quaternion.identity;
				var defaultIntensity = 1f;
				switch (light.Type)
				{
					case LightType.Directional:
						lightComponent.type = UE.LightType.Directional;
						lightComponent.transform.localRotation = UE.Quaternion.LookRotation(UE.Vector3.down, direction);
						break;

					case LightType.Spot:
						lightComponent.type = UE.LightType.Spot;
						lightComponent.spotAngle = (float)light.SpotOuterAngle.Radians * Mathf.Rad2Deg;
						lightComponent.innerSpotAngle = (float)light.SpotInnerAngle.Radians * Mathf.Rad2Deg;

						lightComponent.range = (float)light.AttenuationRange;
						defaultIntensity = GetIntensity(light);

						defaultLightDirection = UE.Quaternion.Euler(90, 0, 0);
						break;

					case LightType.Point:
					default:
						lightComponent.type = UE.LightType.Point;
						lightComponent.range = (float)light.AttenuationRange;
						defaultIntensity = GetIntensity(light);
						break;
				}

				lightComponent.intensity = defaultIntensity * (float)light.Intensity;

				var (localPosition, localRotation) = light.RawPose.ToUnity();
				newLightObject.transform.localPosition = localPosition;
				newLightObject.transform.localRotation *= localRotation * defaultLightDirection;
			}
		}
	}
}