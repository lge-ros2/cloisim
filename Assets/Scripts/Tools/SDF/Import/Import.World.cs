/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private const string SceneVisualRootName = "SceneVisuals";
			private const string SceneGridName = "SceneGrid";
			private const string SceneOriginVisualName = "SceneOriginVisual";
			private const float SceneVisualHeightOffset = 0.01f;
			private const float SceneVisualLineWidth = 0.02f;
			private const int SceneGridHalfExtent = 10;
			private static readonly UE.Color SceneGridColor = new UE.Color(0.55f, 0.55f, 0.55f, 0.7f);
			private static readonly UE.Color SceneGridAxisColor = new UE.Color(0.72f, 0.72f, 0.72f, 0.8f);

			private static UE.Material _sceneVisualMaterial = null;

			private static UE.Material GetSceneVisualMaterial()
			{
				if (_sceneVisualMaterial == null)
				{
					var shader = UE.Shader.Find("Sprites/Default");
					if (shader == null)
					{
						return null;
					}

					_sceneVisualMaterial = new UE.Material(shader)
					{
						hideFlags = UE.HideFlags.DontUnloadUnusedAsset
					};
				}

				return _sceneVisualMaterial;
			}

			private static UE.GameObject CreateOrGetChild(in UE.Transform parent, in string name)
			{
				var child = parent.Find(name);
				if (child != null)
				{
					return child.gameObject;
				}

				var childObject = new UE.GameObject(name);
				childObject.transform.SetParent(parent, false);
				return childObject;
			}

			private static void ConfigureSceneLine(in UE.LineRenderer lineRenderer, in UE.Color color)
			{
				lineRenderer.sharedMaterial = GetSceneVisualMaterial();
				lineRenderer.useWorldSpace = false;
				lineRenderer.alignment = UE.LineAlignment.TransformZ;
				lineRenderer.widthMultiplier = SceneVisualLineWidth;
				lineRenderer.positionCount = 2;
				lineRenderer.startColor = color;
				lineRenderer.endColor = color;
				lineRenderer.shadowCastingMode = UE.Rendering.ShadowCastingMode.Off;
				lineRenderer.receiveShadows = false;
				lineRenderer.lightProbeUsage = UE.Rendering.LightProbeUsage.Off;
				lineRenderer.reflectionProbeUsage = UE.Rendering.ReflectionProbeUsage.Off;
				lineRenderer.motionVectorGenerationMode = UE.MotionVectorGenerationMode.ForceNoMotion;
			}

			private static void CreateLine(in UE.Transform parent, in string name, in UE.Vector3 start, in UE.Vector3 end, in UE.Color color)
			{
				var lineObject = CreateOrGetChild(parent, name);
				var lineRenderer = lineObject.GetComponent<UE.LineRenderer>();
				if (lineRenderer == null)
				{
					lineRenderer = lineObject.AddComponent<UE.LineRenderer>();
				}

				ConfigureSceneLine(lineRenderer, color);
				lineRenderer.SetPosition(0, start);
				lineRenderer.SetPosition(1, end);
			}

			private static void CreateSceneGridVisual(in UE.Transform parent)
			{
				var gridObject = CreateOrGetChild(parent, SceneGridName);
				var gridTransform = gridObject.transform;
				for (var index = -SceneGridHalfExtent; index <= SceneGridHalfExtent; index++)
				{
					var color = index == 0 ? SceneGridAxisColor : SceneGridColor;
					CreateLine(
						gridTransform,
						$"GridX_{index}",
						new UE.Vector3(-SceneGridHalfExtent, SceneVisualHeightOffset, index),
						new UE.Vector3(SceneGridHalfExtent, SceneVisualHeightOffset, index),
						color);
					CreateLine(
						gridTransform,
						$"GridZ_{index}",
						new UE.Vector3(index, SceneVisualHeightOffset, -SceneGridHalfExtent),
						new UE.Vector3(index, SceneVisualHeightOffset, SceneGridHalfExtent),
						color);
				}
			}

			private static void CreateSceneOriginVisual(in UE.Transform parent)
			{
				var originObject = CreateOrGetChild(parent, SceneOriginVisualName);
				var originTransform = originObject.transform;
				CreateLine(originTransform, "AxisX", UE.Vector3.zero, UE.Vector3.right * 1.5f, UE.Color.red);
				CreateLine(originTransform, "AxisY", UE.Vector3.zero, UE.Vector3.up * 1.5f, UE.Color.green);
				CreateLine(originTransform, "AxisZ", UE.Vector3.zero, UE.Vector3.forward * 1.5f, UE.Color.blue);
			}

			private static void ApplyFogSettings(in Fog fogSettings)
			{
				if (fogSettings == null || string.Equals(fogSettings.FogType, "none", StringComparison.OrdinalIgnoreCase))
				{
					UE.RenderSettings.fog = false;
					return;
				}

				UE.RenderSettings.fog = true;
				UE.RenderSettings.fogColor = fogSettings.FogColor.ToUnity();
				UE.RenderSettings.fogStartDistance = (float)fogSettings.Start;
				UE.RenderSettings.fogEndDistance = (float)fogSettings.End;
				UE.RenderSettings.fogDensity = (float)fogSettings.Density;

				switch (fogSettings.FogType?.ToLowerInvariant())
				{
					case "linear":
						UE.RenderSettings.fogMode = UE.FogMode.Linear;
						break;

					case "constant":
						UE.Debug.LogWarning("SDF scene fog type 'constant' is approximated with Unity exponential fog.");
						UE.RenderSettings.fogMode = UE.FogMode.Exponential;
						break;

					case "quadratic":
					default:
						UE.RenderSettings.fogMode = UE.FogMode.ExponentialSquared;
						break;
				}
			}

			private void ApplySceneSettings(in Scene scene)
			{
				_sceneShadowsEnabled = scene.Shadows;
				_sceneSkySettings = scene.SkySettings;
				_sceneSkyAppliedToDirectionalLight = false;
				UE.QualitySettings.shadows = _sceneShadowsEnabled ? UE.ShadowQuality.All : UE.ShadowQuality.Disable;
				UE.RenderSettings.ambientMode = UE.Rendering.AmbientMode.Flat;
				UE.RenderSettings.ambientLight = scene.Ambient.ToUnity();

				if (UE.Camera.main != null)
				{
					UE.Camera.main.clearFlags = UE.CameraClearFlags.SolidColor;
					UE.Camera.main.backgroundColor = scene.Background.ToUnity();
				}

				ApplyFogSettings(scene.FogSettings);

				var sceneVisualRoot = CreateOrGetChild(Main.WorldRoot.transform, SceneVisualRootName).transform;
				if (scene.Element?.FindElement("grid") != null && scene.Grid)
				{
					CreateSceneGridVisual(sceneVisualRoot);
				}

				if (scene.Element?.FindElement("origin_visual") != null && scene.OriginVisual)
				{
					CreateSceneOriginVisual(sceneVisualRoot);
				}
			}

			protected override object ImportWorld(in World world)
			{
				if (world == null)
				{
					return null;
				}

				// Debug.Log("Import World");
				if (world.GuiInfo != null)
				{
					var mainCamera = UE.Camera.main;
					var guiCamera = world.Element?.FindElement("gui")?.FindElement("camera");
					if (mainCamera != null && guiCamera != null)
					{
						var cameraPoseStr = guiCamera.FindElement("pose")?.Value?.GetAsString();
						Math.Pose3d cameraPose = Math.Pose3d.Zero;
						if (!string.IsNullOrEmpty(cameraPoseStr))
						{
							cameraPose = Math.Pose3d.Parse(cameraPoseStr);
						}

						var viewController = guiCamera.FindElement("view_controller")?.Value?.GetAsString() ?? string.Empty;
						var projectionType = guiCamera.FindElement("projection_type")?.Value?.GetAsString() ?? string.Empty;

						var isOrbitControl = viewController.Equals("orbit") ||
							(!viewController.Equals("ortho"));

						if (projectionType.Equals("orthographic"))
						{
							Main.SetCameraOrthographic(!isOrbitControl);
							Main.UIController?.ChangeCameraViewMode(UIController.CameraViewModeEnum.Orthographic);
						}
						else if (projectionType.Equals("perspective"))
						{
							Main.SetCameraPerspective(isOrbitControl);
							Main.UIController?.ChangeCameraViewMode(UIController.CameraViewModeEnum.Perspective);
						}
						else
						{
							UE.Debug.LogWarning($"{projectionType} is not supported. Default value is set to 'perspective'");
							Main.UIController?.ChangeCameraViewMode(UIController.CameraViewModeEnum.Perspective);
						}

						var (cameraPosition, cameraRotation) = cameraPose.ToUnity();
						mainCamera.transform.localPosition = cameraPosition;
						mainCamera.transform.localRotation = cameraRotation;

						var trackVisualElem = guiCamera.FindElement("track_visual");
						if (trackVisualElem != null)
						{
							var trackName = trackVisualElem.FindElement("name")?.Value?.GetAsString() ?? "__default__";
							var trackUseModelFrame = Extensions.GetElementValue(trackVisualElem, "use_model_frame", false);
							var trackStatic = Extensions.GetElementValue(trackVisualElem, "static", false);
							var trackInheritYaw = Extensions.GetElementValue(trackVisualElem, "inherit_yaw", false);
							var trackXyzStr = trackVisualElem.FindElement("xyz")?.Value?.GetAsString() ?? string.Empty;

							if (!trackName.Equals("__default__") &&
								!string.IsNullOrEmpty(trackName) &&
								trackUseModelFrame)
							{
								Main.TrackVisualModelName = trackName;
							}

							if (trackStatic && trackUseModelFrame && !string.IsNullOrEmpty(trackXyzStr))
							{
								Main.TrackVisualPosition = Math.Vector3d.Parse(trackXyzStr).ToUnity();
								Main.TrackVisualInheritYaw = trackInheritYaw;
							}
						}
					}

					if (mainCamera != null)
					{
						Main.CameraInitPose = new UE.Pose(mainCamera.transform.localPosition, mainCamera.transform.localRotation);
					}

					// Skip screen resolution changes in headless/batchmode where
					// Screen.resolutions is empty and there is no display surface.
					if (!UE.Application.isBatchMode)
					{
						var fullscreen = world.GuiInfo.Fullscreen;
						UE.Screen.fullScreen = fullscreen;
						if (fullscreen)
						{
							var currentResolution = UE.Screen.currentResolution;
							UE.Screen.SetResolution(currentResolution.width, currentResolution.height, UE.FullScreenMode.MaximizedWindow);
						}
						else
						{
							var resolutions = UE.Screen.resolutions;
							if (resolutions.Length > 0)
							{
								var resolutionIndex = resolutions.Length / 2;
								var selectedResolution = resolutions[resolutionIndex];
								UE.Screen.SetResolution(selectedResolution.width, selectedResolution.height, UE.FullScreenMode.Windowed);
							}
						}
					}
				}

				if (world.SphericalCoordinatesInfo != null)
				{
					var sphericalCoordinatesCore = DeviceHelper.GetGlobalSphericalCoordinates();

					var sphericalCoordinates = world.SphericalCoordinatesInfo;

					sphericalCoordinatesCore.SetSurfaceType(sphericalCoordinates.Surface.ToString());

					sphericalCoordinatesCore.SetWorldOrientation("ENU");

					sphericalCoordinatesCore.SetCoordinatesReference(
						(float)sphericalCoordinates.LatitudeDeg,
						(float)sphericalCoordinates.LongitudeDeg,
						(float)sphericalCoordinates.ElevationM,
						SDF2Unity.CurveOrientationAngle((float)sphericalCoordinates.HeadingDeg));
				}

				ImportRoads(world);

				UE.Physics.gravity = world.Gravity.ToUnity();
				_sceneShadowsEnabled = true;
				_sceneSkySettings = null;
				_sceneSkyAppliedToDirectionalLight = false;

				// Apply wind if defined
				if (!world.WindLinearVelocity.Equals(Math.Vector3d.Zero))
				{
					var windZone = Main.WorldRoot?.GetComponentInChildren<UE.WindZone>();
					if (windZone == null)
					{
						var windObj = new UE.GameObject("Wind");
						windObj.transform.SetParent((Main.WorldRoot as UE.GameObject)?.transform, false);
						windZone = windObj.AddComponent<UE.WindZone>();
						windZone.mode = UE.WindZoneMode.Directional;
					}
					var windVelocity = world.WindLinearVelocity.ToUnity();
					windZone.transform.forward = windVelocity.normalized;
					windZone.windMain = windVelocity.magnitude;
				}

				// Apply scene settings (ambient, background, fog, shadows)
				if (world.SceneInfo != null)
				{
					ApplySceneSettings(world.SceneInfo);
				}

				ImportLights(world.Lights, _rootLights);

				return Main.WorldRoot;
			}
		}
	}
}