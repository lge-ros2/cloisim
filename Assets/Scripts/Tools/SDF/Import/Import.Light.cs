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

			static private UE.Quaternion GetDirectionalLightRotation(in UE.Vector3 direction)
			{
				var forward = direction.sqrMagnitude > Mathf.Epsilon ? direction.normalized : UE.Vector3.down;
				var up = Mathf.Abs(UE.Vector3.Dot(forward, UE.Vector3.up)) > 0.999f ? UE.Vector3.forward : UE.Vector3.up;
				return UE.Quaternion.LookRotation(forward, up);
			}

			static private UE.Vector3 GetSunriseDirectionFromHeading(in float headingRadians)
			{
				var unityHeadingDegrees = SDF2Unity.CurveOrientation(headingRadians);
				return (UE.Quaternion.Euler(0f, unityHeadingDegrees, 0f) * UE.Vector3.forward).normalized;
			}

			private bool TryGetSceneSunriseDirection(out UE.Vector3 sunriseDirection)
			{
				sunriseDirection = UE.Vector3.forward;

				if (_sceneSkySettings?.Element == null)
				{
					return false;
				}

				var skyElement = _sceneSkySettings.Element;
				var headingDegreeElement = skyElement.FindElement("heading_deg");
				if (headingDegreeElement?.Value != null)
				{
					sunriseDirection = GetSunriseDirectionFromHeading(Mathf.Deg2Rad * (float)headingDegreeElement.Value.DoubleValue);
					return true;
				}

				var headingElement = skyElement.FindElement("heading");
				if (headingElement?.Value != null)
				{
					sunriseDirection = GetSunriseDirectionFromHeading((float)headingElement.Value.DoubleValue);
					return true;
				}

				var cloudDirectionElement = skyElement.FindElement("clouds")?.FindElement("direction");
				if (cloudDirectionElement?.Value != null)
				{
					sunriseDirection = GetSunriseDirectionFromHeading((float)_sceneSkySettings.CloudDirection.Radians);
					return true;
				}

				return false;
			}

			private bool TryGetSceneSunState(in UE.Vector3 fallbackDirection, out UE.Vector3 sunDirection, out float intensityScale)
			{
				sunDirection = fallbackDirection.sqrMagnitude > Mathf.Epsilon ? fallbackDirection.normalized : UE.Vector3.down;
				intensityScale = 1f;

				if (_sceneSkySettings == null)
				{
					return false;
				}

				var sunrise = Mathf.Repeat((float)_sceneSkySettings.Sunrise, 24f);
				var sunset = Mathf.Repeat((float)_sceneSkySettings.Sunset, 24f);
				var time = Mathf.Repeat((float)_sceneSkySettings.Time, 24f);
				if (sunset <= sunrise)
				{
					sunset += 24f;
				}

				if (time < sunrise)
				{
					time += 24f;
				}

				var horizonDirection = fallbackDirection - UE.Vector3.Project(fallbackDirection, UE.Vector3.up);
				if (TryGetSceneSunriseDirection(out var configuredSunriseDirection))
				{
					horizonDirection = configuredSunriseDirection;
				}
				else if (horizonDirection.sqrMagnitude <= 0.001f)
				{
					horizonDirection = UE.Vector3.forward;
				}
				horizonDirection.Normalize();

				var sunriseDirection = horizonDirection;
				var noonDirection = UE.Vector3.down;
				var sunsetDirection = -horizonDirection;
				var dayDuration = Mathf.Max(0.01f, sunset - sunrise);

				if (time >= sunrise && time <= sunset)
				{
					var dayProgress = Mathf.Clamp01((time - sunrise) / dayDuration);
					sunDirection = dayProgress < 0.5f
						? UE.Vector3.Slerp(sunriseDirection, noonDirection, dayProgress * 2f)
						: UE.Vector3.Slerp(noonDirection, sunsetDirection, (dayProgress - 0.5f) * 2f);
					intensityScale = Mathf.Clamp01(Mathf.Sin(dayProgress * Mathf.PI));
					return true;
				}

				var nightDuration = Mathf.Max(0.01f, 24f - dayDuration);
				var nightElapsed = time > sunset ? time - sunset : time + 24f - sunset;
				var nightProgress = Mathf.Clamp01(nightElapsed / nightDuration);
				sunDirection = nightProgress < 0.5f
					? UE.Vector3.Slerp(sunsetDirection, UE.Vector3.up, nightProgress * 2f)
					: UE.Vector3.Slerp(UE.Vector3.up, sunriseDirection, (nightProgress - 0.5f) * 2f);
				intensityScale = 0f;
				return true;
			}

			protected override void ImportLight(in Light light, in object parentObject)
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
					lightComponent.shadows = light.CastShadows && _sceneShadowsEnabled ? UE.LightShadows.Hard : UE.LightShadows.None;
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
						if (!_sceneSkyAppliedToDirectionalLight && TryGetSceneSunState(direction, out var sceneSunDirection, out var sceneSunIntensityScale))
						{
							direction = sceneSunDirection;
							defaultIntensity *= sceneSunIntensityScale;
							_sceneSkyAppliedToDirectionalLight = true;
						}

						lightComponent.type = UE.LightType.Directional;
						lightComponent.transform.localRotation = GetDirectionalLightRotation(direction);
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