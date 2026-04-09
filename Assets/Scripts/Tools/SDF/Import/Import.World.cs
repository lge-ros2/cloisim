/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportWorld(in SDFormat.World world)
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
						SDFormat.Math.Pose3d cameraPose = SDFormat.Math.Pose3d.Zero;
						if (!string.IsNullOrEmpty(cameraPoseStr))
						{
							cameraPose = SDFormat.Math.Pose3d.Parse(cameraPoseStr);
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

						mainCamera.transform.localPosition = cameraPose.ToUnityPosition();
						mainCamera.transform.localRotation = cameraPose.ToUnityRotation();

						var trackVisualElem = guiCamera.FindElement("track_visual");
						if (trackVisualElem != null)
						{
							var trackName = trackVisualElem.FindElement("name")?.Value?.GetAsString() ?? "__default__";
							var trackUseModelFrame = SDFormat.Extensions.GetElementValue(trackVisualElem, "use_model_frame", false);
							var trackStatic = SDFormat.Extensions.GetElementValue(trackVisualElem, "static", false);
							var trackInheritYaw = SDFormat.Extensions.GetElementValue(trackVisualElem, "inherit_yaw", false);
							var trackXyzStr = trackVisualElem.FindElement("xyz")?.Value?.GetAsString() ?? string.Empty;

							if (!trackName.Equals("__default__") &&
								!string.IsNullOrEmpty(trackName) &&
								trackUseModelFrame)
							{
								Main.TrackVisualModelName = trackName;
							}

							if (trackStatic && trackUseModelFrame && !string.IsNullOrEmpty(trackXyzStr))
							{
								Main.TrackVisualPosition = SDFormat.Math.Vector3d.Parse(trackXyzStr).ToUnity();
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

				// Apply wind if defined
				if (!world.WindLinearVelocity.Equals(SDFormat.Math.Vector3d.Zero))
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
					UE.RenderSettings.ambientLight = world.SceneInfo.Ambient.ToUnity();

					if (UE.Camera.main != null)
					{
						UE.Camera.main.backgroundColor = world.SceneInfo.Background.ToUnity();
					}

					if (world.SceneInfo.FogSettings != null && world.SceneInfo.FogSettings.FogType != "none")
					{
						UE.RenderSettings.fog = true;
						UE.RenderSettings.fogColor = world.SceneInfo.FogSettings.FogColor.ToUnity();
						UE.RenderSettings.fogStartDistance = (float)world.SceneInfo.FogSettings.Start;
						UE.RenderSettings.fogEndDistance = (float)world.SceneInfo.FogSettings.End;
						UE.RenderSettings.fogDensity = (float)world.SceneInfo.FogSettings.Density;

						if (world.SceneInfo.FogSettings.FogType == "linear")
						{
							UE.RenderSettings.fogMode = UE.FogMode.Linear;
						}
						else
						{
							UE.RenderSettings.fogMode = UE.FogMode.ExponentialSquared;
						}
					}
				}

				ImportLights(world.Lights, _rootLights);

				return Main.WorldRoot;
			}
		}
	}
}