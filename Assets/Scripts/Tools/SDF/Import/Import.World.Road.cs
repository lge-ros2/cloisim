/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private void ImportRoad(in World.Road road)
			{
				var roadObject = Implement.Road.Generate(road);

				Main.SegmentationManager.AttachTag(roadObject.name, roadObject);
				Main.SegmentationManager.UpdateTags();
			}

			private void ImportRoads(IReadOnlyList<World.Road> items)
			{
				foreach (var item in items)
				{
					ImportRoad(item);
				}
			}
		}
	}
}