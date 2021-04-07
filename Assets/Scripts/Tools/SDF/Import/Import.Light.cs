/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportLight(in Light light)
			{
				if (light == null)
				{
					return null;
				}

				var newLightObject = new UnityEngine.GameObject();
				newLightObject.name = light.Name;
				newLightObject.tag = "Light";

				var lightComponent = newLightObject.AddComponent<UnityEngine.Light>();

				lightComponent.transform.SetParent(_rootObjectLights.transform);

				lightComponent.renderMode = UnityEngine.LightRenderMode.ForcePixel;

				lightComponent.shadows = (light.cast_shadow) ? UnityEngine.LightShadows.Hard : UnityEngine.LightShadows.None;
				lightComponent.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Medium;

				lightComponent.color = SDF2Unity.GetColor(light.diffuse);
				// SDF2Unity.GetColor(light.specular);

				switch (light.Type)
				{
					case "directional":
						{
							lightComponent.type = UnityEngine.LightType.Directional;
						}
						break;

					case "spot":
						{
							lightComponent.type = UnityEngine.LightType.Spot;
							lightComponent.spotAngle = (float)light.spot.outer_angle * Mathf.Rad2Deg;
							lightComponent.innerSpotAngle = (float)light.spot.inner_angle * Mathf.Rad2Deg;
							lightComponent.range = (float)light.attenuation.range;
						}
						break;

					case "point":
					default:
						lightComponent.type = UnityEngine.LightType.Spot;
						lightComponent.range = (float)light.attenuation.range;
						break;
				}

				var localPosition = SDF2Unity.GetPosition(light.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(light.direction);

				newLightObject.transform.localPosition = localPosition;
				newLightObject.transform.localRotation = localRotation;

				return newLightObject as System.Object;
			}
		}
	}
}