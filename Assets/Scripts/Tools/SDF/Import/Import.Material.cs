/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UE = UnityEngine;

namespace SDF
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportMaterial(in SDF.Material sdfMaterial, in System.Object parentObject)
			{
				var logs = new StringBuilder();
				var targetObject = (parentObject as UE.GameObject);

				if (targetObject == null || sdfMaterial == null)
				{
					return;
				}

				var meshRenderers = targetObject.GetComponentsInChildren<UE.Renderer>(true);
				foreach (var renderer in meshRenderers)
				{
					sdfMaterial.Apply(renderer, out var outputLogs);
					logs.Append(outputLogs);

					// Turn off high-loading features in renderer as a performance tunig
					renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
					renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
					renderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;
					renderer.allowOcclusionWhenDynamic = true;
				}

				if (logs.Length > 0)
				{
					logs.Insert(0, "SDF.Import.ImportMaterial() - Implementation logs\n");
					UE.Debug.LogWarning(logs.ToString());
				}
			}
		}
	}
}