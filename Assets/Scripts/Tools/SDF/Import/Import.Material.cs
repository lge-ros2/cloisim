/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UE = UnityEngine;

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportMaterial(in Material sdfMaterial, in object parentObject)
			{
				var logs = new StringBuilder();
				var targetObject = parentObject as UE.GameObject;

				if (targetObject == null || sdfMaterial == null)
				{
					return;
				}

				var meshRenderers = targetObject.GetComponentsInChildren<UE.Renderer>();
				foreach (var renderer in meshRenderers)
				{
					sdfMaterial.Apply(renderer, out var outputLogs);
					logs.Append(outputLogs);

					// Turn off high-loading features in renderer as a performance tuning
					renderer.lightProbeUsage = UE.Rendering.LightProbeUsage.Off;
					renderer.reflectionProbeUsage = UE.Rendering.ReflectionProbeUsage.Off;
					renderer.motionVectorGenerationMode = UE.MotionVectorGenerationMode.ForceNoMotion;
					renderer.allowOcclusionWhenDynamic = true;
				}

				if (logs.Length > 0)
				{
					logs.Insert(0, "SDFormat.Import.ImportMaterial() - Implementation logs\n");
					UE.Debug.LogWarning(logs.ToString());
				}
			}
		}
	}
}