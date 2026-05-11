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
			private UE.GameObject _rootLights = null;
			private bool _sceneShadowsEnabled = true;
			private Sky _sceneSkySettings = null;
			private bool _sceneSkyAppliedToDirectionalLight = false;

			public void SetRootLights(in UE.GameObject root)
			{
				_rootLights = root;
			}
		}
	}
}