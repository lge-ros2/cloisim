/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private void ImportRoads(in SDFormat.World world)
			{
				if (world.Element == null)
				{
					return;
				}

				foreach (var roadElement in world.Element.GetElements("road"))
				{
					var roadObject = Implement.Road.Generate(roadElement);
					if (roadObject != null)
					{
						Main.SegmentationManager.AttachTag(roadObject.name, roadObject);
						Main.SegmentationManager.UpdateTags();
					}
				}
			}
		}
	}
}