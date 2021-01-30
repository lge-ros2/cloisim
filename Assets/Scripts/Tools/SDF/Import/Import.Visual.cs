/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private bool enableOptimization = false;

			protected override System.Object ImportVisual(in SDF.Visual visual, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newVisualObject = new UE.GameObject(visual.Name);
				newVisualObject.tag = "Visual";

				SetParentObject(newVisualObject, targetObject);

				var localPosition = SDF2Unity.GetPosition(visual.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(visual.Pose.Rot);

				var visualPlugin = newVisualObject.AddComponent<Helper.Visual>();
				visualPlugin.isCastingShadow = visual.CastShadow;
				visualPlugin.SetPose(localPosition, localRotation);

				return newVisualObject as System.Object;
			}

			protected override void PostImportVisual(in SDF.Visual visual, in System.Object targetObject)
			{
				var visualObject = (targetObject as UE.GameObject);

				// Optimize geometry and materials
				if (visualObject.CompareTag("Visual"))
				{
					if (enableOptimization)
					{
						Implement.Visual.OptimizeMeshes(visualObject);
					}

					// Turn off high-loading features in renderer as a performance tunig
					var meshRenderers = visualObject.GetComponentsInChildren<UE.MeshRenderer>();
					foreach (var meshRenderer in meshRenderers)
					{
						meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
						meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
						meshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;
						meshRenderer.allowOcclusionWhenDynamic = true;
					}

#if UNITY_EDITOR
					// SceneVisibilityManager.instance.ToggleVisibility(visualObject, true);
					SceneVisibilityManager.instance.DisablePicking(visualObject, true);
#endif
				}
			}
		}
	}
}