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
			private UE.GameObject _rootLights = null;
			private UE.GameObject _rootRoads = null;

			public void SetRootLights(in UE.GameObject root)
			{
				_rootLights = root;
			}

			public void SetRootRoads(in UE.GameObject root)
			{
				_rootRoads = root;
			}
		}
	}
}