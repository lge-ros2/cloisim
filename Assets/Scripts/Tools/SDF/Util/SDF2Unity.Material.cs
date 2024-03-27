/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UE = UnityEngine;

public partial class SDF2Unity
{
	private static readonly string _commonShaderName = "Universal Render Pipeline/Simple Lit";
	private static readonly string _speedTreeShaderName = "Universal Render Pipeline/Nature/SpeedTree8";
	public static Shader CommonShader = Shader.Find(_commonShaderName);
	public static Shader SpeedTreeShader = Shader.Find(_speedTreeShaderName);

	public class Material
	{
		public static UE.Material Create(in string materialName = "")
		{
			var newMaterial = new UE.Material(SDF2Unity.CommonShader);

			newMaterial.name = materialName;
			newMaterial.renderQueue = (int)RenderQueue.Background;

			newMaterial.SetFloat("_Cull", (float)CullMode.Back); // Render face front
			newMaterial.SetFloat("_ZWrite", 1);

			newMaterial.SetFloat("_ReceiveShadows", 1f);

			newMaterial.SetColor("_BaseColor", UE.Color.white);

			newMaterial.SetFloat("_SmoothnessSource", 1f); // Specular Alpha (0) vs Albedo Alpha (1)
			var specularSmoothness = 0.45f;
			SetSpecular(newMaterial, specularSmoothness);

			newMaterial.SetFloat("_EnvironmentReflections", 0.5f);
			newMaterial.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

			newMaterial.EnableKeyword("_INSTANCING_ON");
			newMaterial.EnableKeyword("_DOTS_INSTANCING_ON");

			newMaterial.enableInstancing = true;
			newMaterial.doubleSidedGI = false;

			// newMaterial.hideFlags |= HideFlags.NotEditable;

			return newMaterial;
		}

		private static void SetTransparent(UE.Material target)
		{
			target.SetOverrideTag("RenderType", "Transparent");
			target.SetFloat("_Surface", 1); // set to transparent

			target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
			target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
			target.SetFloat("_AlphaClip", 0);
			target.SetFloat("_QueueOffset", 1);

			target.DisableKeyword("_ALPHABLEND_ON");
			target.renderQueue = (int)RenderQueue.Transparent;
		}

		private static void SetOpaque(UE.Material target)
		{
			target.SetOverrideTag("RenderType", "Opaque");
			target.SetFloat("_Surface", 0); // set to opaque

			target.DisableKeyword("_ALPHABLEND_ON");
			target.renderQueue = (int)RenderQueue.Geometry;
		}

		public static void ConvertToSpeedTree(UE.Material target)
		{
			var existingTexture = target.GetTexture("_BaseMap");
			var existingTextureScale = target.GetTextureScale("_BaseMap");
			var existingColor = target.GetColor("_BaseColor");

			target.shader = SpeedTreeShader;
			target.SetTexture("_MainTex", existingTexture);
			target.SetTextureScale("_MainTex", existingTextureScale);
			target.SetColor("_Color", existingColor);
			target.SetFloat("_Glossiness", 0f);
			target.SetInt("_TwoSided", 0);

			target.SetFloat("_BillboardKwToggle", 0);
			target.SetFloat("_BillSboardShadowFade", 0f);
			target.DisableKeyword("EFFECT_BILLBOARD");

			target.EnableKeyword("_INSTANCING_ON");
		}

		public static void SetBaseColor(UE.Material target, Color color)
		{
			target.SetColor("_BaseColor", color);

			if (color.a < 1)
			{
				SDF2Unity.Material.SetTransparent(target);
			}
			else
			{
				SDF2Unity.Material.SetOpaque(target);
			}
		}

		public static void SetEmission(UE.Material target, Color color)
		{
			target.SetColor("_EmissionColor", color);
			target.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
			target.EnableKeyword("_EMISSION");
		}

		public static void SetSpecular(UE.Material target, in float smoothness = 0.5f)
		{
			var defaultSpecColor = Color.gray;
			defaultSpecColor.a = smoothness;
			SetSpecular(target, defaultSpecColor);
		}

		public static void SetSpecular(UE.Material target, Color color)
		{
			target.SetColor("_SpecColor", color);
			target.SetFloat("_Smoothness", color.a);
			target.SetFloat("_SpecularHighlights", 1f);
			target.EnableKeyword("_SPECGLOSSMAP");
		}
	}
}