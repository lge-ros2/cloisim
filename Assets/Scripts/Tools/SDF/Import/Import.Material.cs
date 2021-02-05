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
							var ambientFinalColor = new UE.Color();
							ambientFinalColor.r = (float)(sdfMaterial.ambient.R);
							ambientFinalColor.g = (float)(sdfMaterial.ambient.G);
							ambientFinalColor.b = (float)(sdfMaterial.ambient.B);
							ambientFinalColor.a = (float)(sdfMaterial.ambient.A);
							// sharedMaterial.SetColor("_Color", ambientFinalColor);
						}

						if (sdfMaterial.diffuse != null)
						{
							const float adjustAlphaRate = 0.15f;
							var diffuseFinalColor = new UE.Color();
							diffuseFinalColor.r = (float)(sdfMaterial.diffuse.R);
							diffuseFinalColor.g = (float)(sdfMaterial.diffuse.G);
							diffuseFinalColor.b = (float)(sdfMaterial.diffuse.B);
							diffuseFinalColor.a = (float)(sdfMaterial.diffuse.A) * adjustAlphaRate;

							sharedMaterial.SetFloat("_Mode", 3f);
							sharedMaterial.SetColor("_Color", diffuseFinalColor);
							sharedMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
							sharedMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
							sharedMaterial.SetFloat("_ZWrite", 0f);
							sharedMaterial.SetFloat("_SmoothnessTextureChannel", 1f);
							sharedMaterial.SetFloat("_Glossiness", 0.0f);
							sharedMaterial.SetFloat("_GlossMapScale", 0.347f);
							sharedMaterial.SetFloat("_SpecularHighlights", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetFloat("_GlossyReflections", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetColor("_SpecColor", new UE.Color(0, 0, 0, 0));
							sharedMaterial.DisableKeyword("_EMISSION");
							sharedMaterial.DisableKeyword("_ALPHATEST_ON");
							sharedMaterial.DisableKeyword("_ALPHABLEND_ON");
							sharedMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
							sharedMaterial.renderQueue = 3000;
						}

						if (sdfMaterial.emissive != null)
						{
							const float intensityRate = 0.3f;
							var emissiveFinalColor = new UE.Color(1, 1, 1, 1);
							emissiveFinalColor.r = (float)(sdfMaterial.emissive.R) * intensityRate;
							emissiveFinalColor.g = (float)(sdfMaterial.emissive.G) * intensityRate;
							emissiveFinalColor.b = (float)(sdfMaterial.emissive.B) * intensityRate;
							emissiveFinalColor.a = (float)(sdfMaterial.emissive.A) * intensityRate;

							sharedMaterial.SetFloat("_Mode", 0f);
							sharedMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
							sharedMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
							sharedMaterial.SetFloat("_ZWrite", 1f);
							sharedMaterial.SetFloat("_SmoothnessTextureChannel", 1);
							sharedMaterial.SetFloat("_Glossiness", 0.6f);
							sharedMaterial.SetFloat("_GlossMapScale", 0.6f);
							sharedMaterial.SetFloat("_SpecularHighlights", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetFloat("_GlossyReflections", 0f); // 0:OFF, 1:ON
							sharedMaterial.SetColor("_SpecColor", new UE.Color(0, 0, 0, 0));
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
							var specularFinalColor = new UE.Color(1, 1, 1, 1);
							specularFinalColor.r = (float)(sdfMaterial.specular.R);
							specularFinalColor.g = (float)(sdfMaterial.specular.G);
							specularFinalColor.b = (float)(sdfMaterial.specular.B);
							specularFinalColor.a = (float)(sdfMaterial.specular.A);
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