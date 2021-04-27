/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportWorld(in SDF.World world)
			{
				if (world == null)
				{
					return;
				}

				// Debug.Log("Import World");
				if (world.gui != null)
				{
					var mainCamera = UnityEngine.Camera.main;
					if (mainCamera != null)
					{
						var cameraPose = world.gui.camera.Pose;
						mainCamera.transform.localPosition = SDF2Unity.GetPosition(cameraPose.Pos);
						mainCamera.transform.localRotation = SDF2Unity.GetRotation(cameraPose.Rot);
					}
				}

				if (world.spherical_coordinates != null)
				{
					var sphericalCoordinatesCore = DeviceHelper.GetSphericalCoordinates();

					var sphericalCoordinates = world.spherical_coordinates;

					sphericalCoordinatesCore.SetSurfaceType(sphericalCoordinates.surface_model);

					sphericalCoordinatesCore.SetWorldOrientation(sphericalCoordinates.world_frame_orientation);

					sphericalCoordinatesCore.SetCoordinatesReference((float)sphericalCoordinates.latitude_deg, (float)sphericalCoordinates.longitude_deg, (float)sphericalCoordinates.elevation, (float)sphericalCoordinates.heading_deg);
				}

				UnityEngine.Physics.gravity = SDF2Unity.GetDirection(world.gravity);
			}
		}
	}
}