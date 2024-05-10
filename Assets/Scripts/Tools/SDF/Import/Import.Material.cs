/*
 * Copyright (c) 2020 LG Electronics Inc.
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
			protected override void ImportMaterial(in SDF.Material sdfMaterial, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);

				if (targetObject == null || sdfMaterial == null)
				{
					return;
				}

				var meshRenderers = targetObject.GetComponentsInChildren<UE.Renderer>(true);
				foreach (var renderer in meshRenderers)
				{
					Implement.Material.Apply(sdfMaterial, renderer);

					// Turn off high-loading features in renderer as a performance tunig
					renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
					renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
					renderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;
					renderer.allowOcclusionWhenDynamic = true;
				}
			}
		}
	}
}