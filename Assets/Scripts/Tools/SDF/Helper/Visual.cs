/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Visual : Base
		{
			[UE.Header("SDF Properties")]
			public bool isCastingShadow = true;

			new void Awake()
			{
				base.Awake();
			}

			void Start()
			{
				SetShadowMode();
			}

			private void SetShadowMode()
			{
				var shadowCastingMode = (isCastingShadow) ? UE.Rendering.ShadowCastingMode.On : UE.Rendering.ShadowCastingMode.Off;

				foreach (var renderer in GetComponentsInChildren<UE.Renderer>())
				{
					renderer.shadowCastingMode = shadowCastingMode;
					renderer.receiveShadows = isCastingShadow;
				}
			}
		}
	}
}