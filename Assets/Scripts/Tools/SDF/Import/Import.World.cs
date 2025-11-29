/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportWorld(in World world)
			{
				if (world == null)
				{
					return null;
				}

				// Debug.Log("Import World");
				if (world.gui != null)
				{
					var mainCamera = UE.Camera.main;
					if (mainCamera != null && world.gui.camera != null)
					{
						var cameraPose = world.gui.camera.Pose;

						var isOrbitControl = (world.gui.camera.view_controller.Equals("orbit")) ?
							true : (world.gui.camera.view_controller.Equals("ortho") ? false : true);

						if (world.gui.camera.projection_type.Equals("orthographic"))
						{
							Main.SetCameraOrthographic(!isOrbitControl);
							Main.UIController.ChangeCameraViewMode(UIController.CameraViewModeEnum.Orthographic);
						}
						else if (world.gui.camera.projection_type.Equals("perspective"))
						{
							Main.SetCameraPerspective(isOrbitControl);
							Main.UIController.ChangeCameraViewMode(UIController.CameraViewModeEnum.Perspective);
						}
						else
						{
							UE.Debug.LogWarning($"{world.gui.camera.projection_type} is not supported. Default value is set to 'perspective'");
							Main.UIController.ChangeCameraViewMode(UIController.CameraViewModeEnum.Perspective);
						}

						mainCamera.transform.localPosition = cameraPose?.Pos.ToUnity() ?? UE.Vector3.zero;
						mainCamera.transform.localRotation = cameraPose?.Rot.ToUnity() ?? UE.Quaternion.identity;

						var trackVisual = world.gui.camera.track_visual;
						if (trackVisual != null)
						{
							if (!trackVisual.name.Equals("__default__") &&
								!string.IsNullOrEmpty(trackVisual.name) &&
								trackVisual.use_model_frame)
							{
								Main.TrackVisualModelName = trackVisual.name;
							}

							if (trackVisual.static_ &&
								trackVisual.use_model_frame)
							{
								Main.TrackVisualPosition = trackVisual.xyz.ToUnity();
								Main.TrackVisualInheritYaw = trackVisual.inherit_yaw;
							}
						}
					}

					Main.CameraInitPose = new UE.Pose(mainCamera.transform.localPosition, mainCamera.transform.localRotation);

					UE.Screen.fullScreen = world.gui.fullscreen;
					if (world.gui.fullscreen)
					{
						var currentResolution = UE.Screen.currentResolution;
						UE.Screen.SetResolution(currentResolution.width, currentResolution.height, UE.FullScreenMode.MaximizedWindow);
					}
					else
					{
						var resolutionIndex = UE.Screen.resolutions.Length / 2;
						// for (int i = 0; i < UE.Screen.resolutions.Length; i++)
						// 	UE.Debug.Log(UE.Screen.resolutions[i]);
						var selectedResolution = UE.Screen.resolutions[resolutionIndex];
						// UE.Debug.Log($"SelectedWindowResolution={selectedResolution}");

						UE.Screen.SetResolution(selectedResolution.width, selectedResolution.height, UE.FullScreenMode.Windowed);
					}
				}

				if (world.spherical_coordinates != null)
				{
					var sphericalCoordinatesCore = DeviceHelper.GetGlobalSphericalCoordinates();

					var sphericalCoordinates = world.spherical_coordinates;

					sphericalCoordinatesCore.SetSurfaceType(sphericalCoordinates.surface_model);

					sphericalCoordinatesCore.SetWorldOrientation(sphericalCoordinates.world_frame_orientation);

					sphericalCoordinatesCore.SetCoordinatesReference(
						(float)sphericalCoordinates.latitude_deg,
						(float)sphericalCoordinates.longitude_deg,
						(float)sphericalCoordinates.elevation,
						SDF2Unity.CurveOrientationAngle((float)sphericalCoordinates.heading_deg));
				}

				ImportRoads(world.GetRoads());

				UE.Physics.gravity = world.gravity.ToUnity();

				ImportLights(world.GetLights());

				return Main.WorldRoot;
			}
		}
	}
}