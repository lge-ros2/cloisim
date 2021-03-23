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
				if (world.GUI.camera.Pose != null)
				{
					_mainCamera.transform.localPosition = SDF2Unity.GetPosition(world.GUI.camera.Pose.Pos);
					_mainCamera.transform.localRotation = SDF2Unity.GetRotation(world.GUI.camera.Pose.Rot);
				}
			}
		}
	}
}