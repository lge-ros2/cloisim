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

				if (targetObject == null)
				{
					return;
				}

				foreach (var renderer in targetObject.GetComponentsInChildren<UE.Renderer>(true))
				{
					var sharedMaterial = renderer.sharedMaterial;

					if (sharedMaterial == null)
					{
						sharedMaterial = new UE.Material(SDF2Unity.commonShader);
						sharedMaterial.name = renderer.name;
					}

					if (sdfMaterial != null)
					{
						if (sdfMaterial.ambient != null)
						{
							var ambientFinalColor = SDF2Unity.GetColor(sdfMaterial.ambient);
							// sharedMaterial.SetColor("_Color", ambientFinalColor);
						}

						if (sdfMaterial.diffuse != null)
						{
							const float adjustAlphaRate = 0.15f;
							var diffuseFinalColor = SDF2Unity.GetColor(sdfMaterial.diffuse);
							diffuseFinalColor.a *= adjustAlphaRate;

							sharedMaterial.SetFloat("_Mode", 3f);
							sharedMaterial.SetColor("_Color", diffuseFinalColor);
							sharedMaterial.SetFloat("_SrcBlend", (float)UE.Rendering.BlendMode.One);
							sharedMaterial.SetFloat("_DstBlend", (float)UE.Rendering.BlendMode.OneMinusSrcAlpha);
							sharedMaterial.SetFloat("_ZWrite", 0f);
							sharedMaterial.SetFloat("_SmoothnessTextureChannel", 1f);
							sharedMaterial.SetFloat("_Glossiness", 0.0f);
							sharedMaterial.SetFloat("_GlossMapScale", 0.347f);
							sharedMaterial.SetFloat("_SpecularHighlights", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetFloat("_GlossyReflections", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetColor("_SpecColor", UE.Color.clear);
							sharedMaterial.DisableKeyword("_EMISSION");
							sharedMaterial.DisableKeyword("_ALPHATEST_ON");
							sharedMaterial.DisableKeyword("_ALPHABLEND_ON");
							sharedMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
							sharedMaterial.renderQueue = 3000;
						}

						if (sdfMaterial.emissive != null)
						{
							const float intensityRate = 0.3f;
							var emissiveFinalColor = SDF2Unity.GetColor(sdfMaterial.emissive) * intensityRate;

							sharedMaterial.SetFloat("_Mode", 0f);
							sharedMaterial.SetFloat("_SrcBlend", (float)UE.Rendering.BlendMode.One);
							sharedMaterial.SetFloat("_DstBlend", (float)UE.Rendering.BlendMode.Zero);
							sharedMaterial.SetFloat("_ZWrite", 1f);
							sharedMaterial.SetFloat("_SmoothnessTextureChannel", 1);
							sharedMaterial.SetFloat("_Glossiness", 0.6f);
							sharedMaterial.SetFloat("_GlossMapScale", 0.6f);
							sharedMaterial.SetFloat("_SpecularHighlights", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetFloat("_GlossyReflections", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetColor("_SpecColor", UE.Color.clear);
							sharedMaterial.SetColor("_EmissionColor", emissiveFinalColor);
							sharedMaterial.EnableKeyword("_EMISSION");
							sharedMaterial.EnableKeyword("UNITY_HDR_ON");
							sharedMaterial.DisableKeyword("_ALPHATEST_ON");
							sharedMaterial.DisableKeyword("_ALPHABLEND_ON");
							sharedMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
							sharedMaterial.renderQueue = 2000;
						}

						if (sdfMaterial.specular != null)
						{
							var specularFinalColor = SDF2Unity.GetColor(sdfMaterial.specular);
							// sharedMaterial.SetColor("_SpecColor", specularFinalColor);
						}
					}
					sharedMaterial.enableInstancing = true;
					sharedMaterial.hideFlags |= UE.HideFlags.NotEditable;
				}
			}
		}
	}
}