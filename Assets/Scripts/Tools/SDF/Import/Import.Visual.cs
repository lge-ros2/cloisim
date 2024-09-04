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

				targetObject.SetChild(newVisualObject);

				var localPosition = SDF2Unity.Position(visual.Pose?.Pos);
				var localRotation = SDF2Unity.Rotation(visual.Pose?.Rot);

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

				RemoveColliders(visualObject);

				AddRenderes(visualObject);

				if (EnableOptimization)
				{
					if (visualObject.transform.childCount > 0)
					{
						for (var i = 0; i < visualObject.transform.childCount; i++)
						{
							var geometryTransform = visualObject.transform.GetChild(i);
							if (geometryTransform.CompareTag("Geometry"))
							{
								Implement.Visual.OptimizeMeshes(geometryTransform);
							}
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

			private void RemoveColliders(UE.GameObject targetObject)
			{
				var colliders = targetObject.GetComponentsInChildren<UE.Collider>();
				foreach (var collider in colliders)
				{
					UE.Debug.LogWarning($"{collider.name} Collider should not exit. There was collider");
					UE.GameObject.Destroy(collider);
				}
			}

			private void AddRenderes(UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();
				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.gameObject.GetComponent<UE.MeshRenderer>();
					if (meshRenderer == null)
					{
						meshRenderer = meshFilter.gameObject.AddComponent<UE.MeshRenderer>();
						meshRenderer.materials = new UE.Material[] { SDF2Unity.Material.Create(meshFilter.name + "_material") };
						meshRenderer.allowOcclusionWhenDynamic = true;
						meshRenderer.receiveShadows = true;
					}
				}
			}
		}
	}
}