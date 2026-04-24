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

			protected override object ImportVisual(in Visual visual, in object parentObject)
			{
				var targetObject = parentObject as UE.GameObject;
				var newVisualObject = new UE.GameObject(visual.Name)
				{
					tag = "Visual"
				};

				targetObject.SetChild(newVisualObject);

				var visualHelper = newVisualObject.AddComponent<Helper.Visual>();
				visualHelper.isCastingShadow = visual.CastShadows;
				visualHelper.metaLayer = visual.GetMetaLayer();
				visualHelper.Pose = visual.RawPose;
				visualHelper.PoseRelativeTo = visual.PoseRelativeTo;

				return newVisualObject as object;
			}

			protected override void AfterImportVisual(in Visual visual, in object targetObject)
			{
				if (visual == null)
				{
					return;
				}

				var visualObject = targetObject as UE.GameObject;

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