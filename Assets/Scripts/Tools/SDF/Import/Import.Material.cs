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
					foreach (var material in renderer.materials)
					{
						if (sdfMaterial.ambient != null)
						{
							UE.Debug.Log(material.name + ": ambient is not support. " + 	SDF2Unity.GetColor(sdfMaterial.ambient));
						}

						if (sdfMaterial.diffuse != null)
						{
							SDF2Unity.Material.SetBaseColor(material, SDF2Unity.GetColor(sdfMaterial.diffuse));
						}

						if (sdfMaterial.emissive != null)
						{
							SDF2Unity.Material.SetEmission(material, SDF2Unity.GetColor(sdfMaterial.emissive));
						}

						if (sdfMaterial.specular != null)
						{
							SDF2Unity.Material.SetSpecular(material, SDF2Unity.GetColor(sdfMaterial.specular));
							// UE.Debug.Log("ImportMaterial HasColorSpecular " + material.GetColor("_SpecColor"));
						}


						// apply material script
						if (sdfMaterial.script != null)
						{
							if (sdfMaterial.script.name.ToLower().Contains("tree"))
							{
								SDF2Unity.Material.SetSpeedTree(material);
							}

							// Name of material from an installed script file.
							// This will override the color element if the script exists.
							Implement.Visual.ApplyMaterial(sdfMaterial.script, material);
						}
					}

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