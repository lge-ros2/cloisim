/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

public partial class SDFImporter : SDF.Importer
{
	private bool enableOptimization = false;

	protected override System.Object ImportVisual(in SDF.Visual visual, in System.Object parentObject)
	{
		var targetObject = (parentObject as GameObject);
		var newVisualObject = new GameObject(visual.Name);
		SetParentObject(newVisualObject, targetObject);

		var visualPlugin = newVisualObject.AddComponent<VisualPlugin>();
		visualPlugin.isCastingShadow = visual.CastShadow;

		newVisualObject.transform.localPosition = SDF2Unity.GetPosition(visual.Pose.Pos);
		newVisualObject.transform.localRotation = SDF2Unity.GetRotation(visual.Pose.Rot);

		return newVisualObject as System.Object;
	}

	protected override void PostImportVisual(in SDF.Visual visual, in System.Object targetObject)
	{
		var visualObject = (targetObject as GameObject);

		// Optimize geometry and materials
		if (visualObject.CompareTag("Visual"))
		{
			if (enableOptimization)
			{
				SDFImplement.Visual.OptimizeMeshes(visualObject);
			}

			// Turn off high-loading features in renderer as a performance tunig
			var meshRenderers = visualObject.GetComponentsInChildren<MeshRenderer>();
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