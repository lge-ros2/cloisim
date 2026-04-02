/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			private static readonly bool EnableOptimization = true;

			protected override System.Object ImportVisual(in SDFormat.Visual visual, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newVisualObject = new UE.GameObject(visual.Name);
				newVisualObject.tag = "Visual";

				targetObject.SetChild(newVisualObject);

				var localPosition = visual.RawPose.ToUnityPosition();
				var localRotation = visual.RawPose.ToUnityRotation();

				var visualHelper = newVisualObject.AddComponent<Helper.Visual>();
				visualHelper.isCastingShadow = visual.CastShadows;
				visualHelper.metaLayer = visual.GetMetaLayer();
				visualHelper.Pose = visual.RawPose;
				visualHelper.PoseRelativeTo = visual.PoseRelativeTo;

				return newVisualObject as System.Object;
			}

			protected override void AfterImportVisual(in SDFormat.Visual visual, in System.Object targetObject)
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