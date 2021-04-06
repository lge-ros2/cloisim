/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportLight(in Light light)
			{
				if (light == null)
				{
					return null;
				}

				var newLightObject = new UnityEngine.GameObject();



				return newLightObject as System.Object;
			}
		}
	}
}