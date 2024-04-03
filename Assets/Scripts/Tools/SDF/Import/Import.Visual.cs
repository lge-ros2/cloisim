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
			private static readonly bool EnableOptimization = true;

			protected override System.Object ImportVisual(in SDF.Visual visual, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newVisualObject = new UE.GameObject(visual.Name);
				newVisualObject.tag = "Visual";

				SetParentObject(newVisualObject, targetObject);

				var localPosition = SDF2Unity.Position(visual.Pose.Pos);
				var localRotation = SDF2Unity.Rotation(visual.Pose.Rot);

				var visualHelper = newVisualObject.AddComponent<Helper.Visual>();
				visualHelper.isCastingShadow = visual.CastShadow;
				visualHelper.metaLayer = visual.GetMetaLayer();
				visualHelper.SetPose(localPosition, localRotation);
				visualHelper.ResetPose();

				return newVisualObject as System.Object;
			}

			protected override void AfterImportVisual(in SDF.Visual visual, in System.Object targetObject)
			{
				if (visual == null)
				{
					return;
				}

				var visualObject = (targetObject as UE.GameObject);

				// Optimize geometry and materials
				if (visualObject.CompareTag("Visual") == false)
				{
					return;
				}

				// remove all colliders
				var colliders = visualObject.GetComponentsInChildren<UE.Collider>();
				foreach (var collider in colliders)
				{
					UE.GameObject.Destroy(collider);
				}

				if (EnableOptimization)
				{
					if (visualObject.transform.childCount > 0)
					{
						var geometryTransform = visualObject.transform.GetChild(0);
						if (geometryTransform.CompareTag("Geometry"))
						{
							Implement.Visual.OptimizeMeshes(geometryTransform);
						}
					}
					else
					{
						UE.Debug.LogWarning(visualObject.name + " has no geometry object");
					}
				}

#if UNITY_EDITOR
				// SceneVisibilityManager.instance.ToggleVisibility(visualObject, true);
				SceneVisibilityManager.instance.DisablePicking(visualObject, true);
#endif
			}
		}
	}
}