/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.Rendering;
using UE = UnityEngine;

public static partial class SDF2Unity
{
	private static UE.Material _baseHDRPMaterial;

	private static UE.Material GetBaseHDRPMaterial()
	{
		if (_baseHDRPMaterial == null)
		{
			// Load the pre-configured HDRP material from Resources.
			// This avoids magenta issues caused by procedural material creation lacking necessary HDRP serialization data.
			_baseHDRPMaterial = UE.Resources.Load<UE.Material>("Materials/HDRPBase");
			
			if (_baseHDRPMaterial != null)
			{
				UE.Debug.Log($"[SDF2Unity] Successfully loaded base HDRP material from Resources.");
			}
			else
			{
				UE.Debug.LogError("[SDF2Unity] Cannot find Materials/HDRPBase in Resources, falling back to Standard.");
				_baseHDRPMaterial = new UE.Material(UE.Shader.Find("Standard"));
			}
		}
		return _baseHDRPMaterial;
	}

	public static UE.Shader CommonShader
	{
		get { return GetBaseHDRPMaterial().shader; }
	}

	public static UE.Shader SpeedTreeShader
	{
		get { return CommonShader; }
	}

	public static UE.Material CreateMaterial(in string materialName = "")
	{
		// Instantiate a copy of the properly initialized base material
		var newMaterial = new UE.Material(GetBaseHDRPMaterial());

		newMaterial.name = materialName;
		
		newMaterial.SetFloat("_CullMode", (float)CullMode.Back);
		newMaterial.SetFloat("_ZWrite", 1);
		newMaterial.SetFloat("_ReceivesSSR", 1f);
		newMaterial.SetColor("_BaseColor", UE.Color.white);
		newMaterial.SetFloat("_Smoothness", 0.5f);
		newMaterial.DisableKeyword("_NORMALMAP");
		newMaterial.enableInstancing = true;
		newMaterial.doubleSidedGI = false;

		return newMaterial;
	}

	public static void SetTransparent(this UE.Material target)
	{
		target.SetOverrideTag("RenderType", "Transparent");
		target.SetFloat("_SurfaceType", 1); // set to transparent

		target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		target.SetFloat("_AlphaCutoffEnable", 0);

		target.renderQueue = (int)RenderQueue.Transparent;
	}

	public static void SetOpaque(this UE.Material target)
	{
		target.SetOverrideTag("RenderType", "Opaque");
		target.SetFloat("_SurfaceType", 0); // set to opaque

		target.renderQueue = (int)RenderQueue.Geometry;
	}

	public static void ConvertToSpeedTree(this UE.Material target)
	{
		var existingTexture = target.GetTexture("_BaseColorMap");
		var existingTextureScale = target.GetTextureScale("_BaseColorMap");
		var existingColor = target.GetColor("_BaseColor");

		target.shader = SpeedTreeShader;
		target.SetTexture("_BaseColorMap", existingTexture);
		target.SetTextureScale("_BaseColorMap", existingTextureScale);
		target.SetColor("_BaseColor", existingColor);
		target.SetFloat("_Smoothness", 0f);
	}

	public static void SetBaseColor(this UE.Material target, UE.Color color)
	{
		target.SetColor("_BaseColor", color);

		if (color.a < 1)
		{
			target.SetTransparent();
		}
		else
		{
			target.SetOpaque();
		}
	}

	public static void SetEmission(this UE.Material target, UE.Color color)
	{
		target.SetColor("_EmissiveColor", color);
		target.globalIlluminationFlags = UE.MaterialGlobalIlluminationFlags.None;
		target.EnableKeyword("_EMISSION");
	}

	public static void SetSpecular(this UE.Material target, in float smoothness = 0.5f)
	{
		var defaultSpecColor = UE.Color.gray;
		defaultSpecColor.a = smoothness;
		SetSpecular(target, defaultSpecColor);
	}

	public static void SetNormalMap(this UE.Material target, in string normalMapPath)
	{
		var texture = MeshLoader.GetTexture(normalMapPath);
		target.SetTexture("_NormalMap", texture);
		target.EnableKeyword("_NORMALMAP");
	}

	public static void SetSpecular(this UE.Material target, UE.Color color)
	{
		target.SetColor("_SpecularColor", color);
		target.SetFloat("_Smoothness", color.a);
	}
}