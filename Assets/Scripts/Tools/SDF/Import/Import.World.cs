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
					var mainCamera = UnityEngine.Camera.main;
					if (mainCamera != null)
					{
						var cameraPose = world.gui.camera.Pose;
						mainCamera.transform.localPosition = SDF2Unity.Position(cameraPose.Pos);
						mainCamera.transform.localRotation = SDF2Unity.Rotation(cameraPose.Rot);

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
							UE.Debug.Log($"SelectedWindowResolution={selectedResolution}");

							UE.Screen.SetResolution(selectedResolution.width, selectedResolution.height, UE.FullScreenMode.Windowed);
						}
					}
				}

				if (world.spherical_coordinates != null)
				{
					var sphericalCoordinatesCore = DeviceHelper.GetGlobalSphericalCoordinates();

					var sphericalCoordinates = world.spherical_coordinates;

					sphericalCoordinatesCore.SetSurfaceType(sphericalCoordinates.surface_model);

					sphericalCoordinatesCore.SetWorldOrientation(sphericalCoordinates.world_frame_orientation);

					sphericalCoordinatesCore.SetCoordinatesReference((float)sphericalCoordinates.latitude_deg, (float)sphericalCoordinates.longitude_deg, (float)sphericalCoordinates.elevation, (float)sphericalCoordinates.heading_deg);
				}

				ImportRoads(world.GetRoads());

				UnityEngine.Physics.gravity = SDF2Unity.Direction(world.gravity);

				ImportLights(world.GetLights());

				return Main.WorldRoot;
			}
		}
	}
}