/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Mathf = UnityEngine.Mathf;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private const float DefaultLightIntensity = 50;

			protected override void ImportLight(in Light light)
			{
				if (light == null)
				{
					return;
				}

				var newLightObject = new UE.GameObject();
				newLightObject.name = light.Name;
				newLightObject.tag = "Light";

				var lightComponent = newLightObject.AddComponent<UE.Light>();

				lightComponent.transform.SetParent(_rootLights.transform);

				lightComponent.renderMode = UE.LightRenderMode.ForcePixel;

				lightComponent.shadows = (light.cast_shadow) ? UE.LightShadows.Hard : UE.LightShadows.None;
				lightComponent.shadowResolution = UE.Rendering.LightShadowResolution.Medium;

				lightComponent.color = SDF2Unity.GetColor(light.diffuse);
				lightComponent.cullingMask = UE.LayerMask.GetMask("Default") | UE.LayerMask.GetMask("Plane");
				// SDF2Unity.GetColor(light.specular);

				var direction = SDF2Unity.GetDirection(light.direction);

				switch (light.Type)
				{
					case "directional":
						lightComponent.type = UE.LightType.Directional;
						lightComponent.transform.localRotation = UE.Quaternion.LookRotation(UE.Vector3.down, direction);

						break;

					case "spot":
						lightComponent.type = UE.LightType.Spot;
						lightComponent.spotAngle = (float)light.spot.outer_angle * Mathf.Rad2Deg;
						lightComponent.innerSpotAngle = (float)light.spot.inner_angle * Mathf.Rad2Deg;
						lightComponent.range = (float)light.attenuation.range;
						lightComponent.intensity = (light.intensity.Equals(1)) ? DefaultLightIntensity : (float)light.intensity;
						break;

					case "point":
					default:
						lightComponent.type = UE.LightType.Point;
						lightComponent.range = (float)light.attenuation.range;
						lightComponent.transform.localRotation = UE.Quaternion.LookRotation(UE.Vector3.down, direction);
						lightComponent.intensity = (light.intensity.Equals(1)) ? DefaultLightIntensity : (float)light.intensity;
						break;
				}

				var localPosition = SDF2Unity.GetPosition(light.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(light.Pose.Rot);

				newLightObject.transform.localPosition = localPosition;
				newLightObject.transform.localRotation *= localRotation;
			}
		}
	}
}