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
	using Implement;

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

				var localPosition = visual.Pose?.Pos.ToUnity() ?? UE.Vector3.zero;
				var localRotation = visual.Pose?.Rot.ToUnity() ?? UE.Quaternion.identity;

				var visualHelper = newVisualObject.AddComponent<Helper.Visual>();
				visualHelper.isCastingShadow = visual.CastShadow;
				visualHelper.metaLayer = visual.GetMetaLayer();
				visualHelper.Pose = visual?.Pose;

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

				visualObject.RemoveColliders();
				visualObject.AddRenderes();

				if (EnableOptimization)
				{
					if (visualObject.transform.childCount > 0)
					{
						for (var i = 0; i < visualObject.transform.childCount; i++)
						{
							var geometryTransform = visualObject.transform.GetChild(i);
							if (geometryTransform.CompareTag("Geometry"))
							{
								geometryTransform.OptimizeMeshes();
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
		}
	}
}