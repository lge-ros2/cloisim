/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.Rendering;
using UE = UnityEngine;

public partial class SDF2Unity
{
	public class Material
	{
		private static readonly string _commonShaderName = "Universal Render Pipeline/Simple Lit";
		private static readonly string _speedTreeShaderName = "Universal Render Pipeline/Nature/SpeedTree8";
		public static UE.Shader CommonShader = UE.Shader.Find(_commonShaderName);
		public static UE.Shader SpeedTreeShader = UE.Shader.Find(_speedTreeShaderName);

		public static UE.Material Create(in string materialName = "")
		{
			var newMaterial = new UE.Material(CommonShader);

			newMaterial.name = materialName;
			newMaterial.renderQueue = (int)RenderQueue.Background;

			newMaterial.SetFloat("_Cull", (float)CullMode.Back); // Render face front
			newMaterial.SetFloat("_ZWrite", 1);

			newMaterial.SetFloat("_ReceiveShadows", 1f);

			newMaterial.SetColor("_BaseColor", UE.Color.white);

			// var specularSmoothness = 0.35f;
			// SetSpecular(newMaterial, specularSmoothness);

			newMaterial.SetFloat("_EnvironmentReflections", 0.5f);
			newMaterial.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

			newMaterial.DisableKeyword("_NORMALMAP");

			newMaterial.EnableKeyword("_INSTANCING_ON");
			newMaterial.EnableKeyword("_DOTS_INSTANCING_ON");
			newMaterial.DisableKeyword("_SPECGLOSSMAP");

			newMaterial.enableInstancing = true;
			newMaterial.doubleSidedGI = false;

			// newMaterial.hideFlags |= HideFlags.NotEditable;

			return newMaterial;
		}

		public static void SetTransparent(UE.Material target)
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

		public static void SetOpaque(UE.Material target)
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

		public static void SetBaseColor(UE.Material target, UE.Color color)
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

		public static void SetEmission(UE.Material target, UE.Color color)
		{
			target.SetColor("_EmissionColor", color);
			target.globalIlluminationFlags = UE.MaterialGlobalIlluminationFlags.None;
			target.EnableKeyword("_EMISSION");
		}

		public static void SetSpecular(UE.Material target, in float smoothness = 0.5f)
		{
			var defaultSpecColor = UE.Color.gray;
			defaultSpecColor.a = smoothness;
			SetSpecular(target, defaultSpecColor);
		}

		public static void SetNormalMap(UE.Material target, in string normalMapPath)
		{
			var texture = MeshLoader.GetTexture(normalMapPath);
			target.SetTexture("_BumpMap", texture);
			target.EnableKeyword("_NORMALMAP");
		}

		public static void SetSpecular(UE.Material target, UE.Color color)
		{
			target.SetFloat("_SmoothnessSource", 0f); // Specular Alpha (0) vs Albedo Alpha (1)
			target.SetColor("_SpecColor", color);
			target.SetFloat("_Smoothness", color.a);
			target.SetFloat("_SpecularHighlights", 1f);
			target.EnableKeyword("_SPECGLOSSMAP");
		}
	}
}