/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.Rendering;
using UE = UnityEngine;

public static partial class SDF2Unity
{
	private const float SpecularWorkflowMode = 0f;
	private const float MetallicWorkflowMode = 1f;
	private const float OpaqueSurfaceType = 0f;
	private const float TransparentSurfaceType = 1f;
	private const float SmoothnessTextureChannelSpecularOrMetallicAlpha = 0f;
	private const float SmoothnessTextureChannelAlbedoAlpha = 1f;

	private static readonly string _commonShaderName = "Custom/URP/Lit";
	private static readonly string _speedTreeShaderName = "Universal Render Pipeline/Nature/SpeedTree8";
	public static UE.Shader CommonShader = UE.Shader.Find(_commonShaderName);
	public static UE.Shader SpeedTreeShader = UE.Shader.Find(_speedTreeShaderName);

	private static void SetKeyword(this UE.Material target, in string keyword, in bool enabled)
	{
		CoreUtils.SetKeyword(target, new LocalKeyword(target.shader, keyword), enabled);
	}

	private static bool HasTexture(this UE.Material target, in string propertyName)
	{
		if (!target.HasProperty(propertyName))
		{
			return false;
		}

		var texture = target.GetTexture(propertyName);
		return texture != null && texture != UE.Texture2D.whiteTexture && texture != UE.Texture2D.normalTexture;
	}

	private static void SyncLegacyBaseMap(this UE.Material target)
	{
		if (!target.HasProperty("_BaseMap"))
		{
			return;
		}

		var hasBaseMap = target.HasTexture("_BaseMap");
		var hasLegacyMainTex = target.HasTexture("_MainTex");

		if (!hasBaseMap && hasLegacyMainTex)
		{
			target.SetTexture("_BaseMap", target.GetTexture("_MainTex"));
			target.SetTextureScale("_BaseMap", target.GetTextureScale("_MainTex"));
			target.SetTextureOffset("_BaseMap", target.GetTextureOffset("_MainTex"));
		}
		else if (hasBaseMap && !hasLegacyMainTex)
		{
			target.SetTexture("_MainTex", target.GetTexture("_BaseMap"));
			target.SetTextureScale("_MainTex", target.GetTextureScale("_BaseMap"));
			target.SetTextureOffset("_MainTex", target.GetTextureOffset("_BaseMap"));
		}
	}

	private static void SyncLegacyBaseColor(this UE.Material target)
	{
		if (!target.HasProperty("_BaseColor") || !target.HasProperty("_Color"))
		{
			return;
		}

		var baseColor = target.GetColor("_BaseColor");
		var legacyColor = target.GetColor("_Color");

		if (baseColor != legacyColor)
		{
			target.SetColor("_Color", baseColor);
		}
	}

	private static bool HasEmission(this UE.Material target)
	{
		if (!target.HasProperty("_EmissionColor"))
		{
			return false;
		}

		var emissionColor = target.GetColor("_EmissionColor");
		return emissionColor.maxColorComponent > 0f;
	}

	private static bool IsOpaqueSurface(this UE.Material target)
	{
		return !target.HasProperty("_Surface") || UE.Mathf.Approximately(target.GetFloat("_Surface"), OpaqueSurfaceType);
	}

	public static void UseMetallicWorkflow(this UE.Material target)
	{
		target.SetFloat("_WorkflowMode", MetallicWorkflowMode);
		target.DisableKeyword("_SPECULAR_SETUP");
	}

	public static void UseSpecularWorkflow(this UE.Material target)
	{
		target.SetFloat("_WorkflowMode", SpecularWorkflowMode);
		target.EnableKeyword("_SPECULAR_SETUP");
	}

	public static void RefreshLitKeywords(this UE.Material target)
	{
#if UNITY_EDITOR
		target.SyncLegacyBaseMap();
		target.SyncLegacyBaseColor();

		// Call Unity's own Lit shader ValidateMaterial — the exact method
		// that runs when switching shaders in the Inspector.
		ValidateLitMaterial(target);
		target.SyncLegacyBaseMap();
		target.SyncLegacyBaseColor();

		// Keep compatibility with the project material contract used by tests and importers.
		target.SetKeyword("_EMISSION", target.HasEmission());
#else
		RefreshLitKeywordsManual(target);
#endif
	}

#if UNITY_EDITOR
	private static System.Action<UE.Material> _cachedValidate;

	private static void ValidateLitMaterial(UE.Material target)
	{
		if (_cachedValidate == null)
		{
			_cachedValidate = CreateLitMaterialValidator();
		}

		_cachedValidate(target);
	}

	private static System.Action<UE.Material> CreateLitMaterialValidator()
	{
		var editorAsm = FindLoadedAssembly("Unity.RenderPipelines.Universal.Editor");
		if (editorAsm == null)
		{
			return RefreshLitKeywordsManual;
		}

		// BaseShaderGUI is declared in the UnityEditor namespace, not under URP's ShaderGUI namespace.
		var baseType = editorAsm.GetType("UnityEditor.BaseShaderGUI");
		var litType = editorAsm.GetType("UnityEditor.Rendering.Universal.ShaderGUI.LitGUI");
		if (baseType == null || litType == null)
		{
			return RefreshLitKeywordsManual;
		}

		var setKwMethod = baseType.GetMethod(
			"SetMaterialKeywords",
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
			null,
			new[] { typeof(UE.Material), typeof(System.Action<UE.Material>), typeof(System.Action<UE.Material>) },
			null);
		var litKwMethod = litType.GetMethod(
			"SetMaterialKeywords",
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
			null,
			new[] { typeof(UE.Material) },
			null);
		if (setKwMethod == null || litKwMethod == null)
		{
			return RefreshLitKeywordsManual;
		}

		var litAction = (System.Action<UE.Material>)System.Delegate.CreateDelegate(
			typeof(System.Action<UE.Material>), litKwMethod);

		return mat =>
		{
			setKwMethod.Invoke(null, new object[] { mat, litAction, null });
		};
	}

	private static System.Reflection.Assembly FindLoadedAssembly(string assemblyName)
	{
		foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
		{
			if (assembly.GetName().Name == assemblyName)
			{
				return assembly;
			}
		}

		try
		{
			return System.Reflection.Assembly.Load(assemblyName);
		}
		catch (System.Exception)
		{
			return null;
		}
	}
#endif

	private static void RefreshLitKeywordsManual(this UE.Material target)
	{
		target.SyncLegacyBaseMap();
		target.SyncLegacyBaseColor();

		if (target.HasProperty("_SpecularHighlights") && target.GetFloat("_SpecularHighlights") < 0.5f)
		{
			target.SetFloat("_SpecularHighlights", 1f);
		}

		if (target.HasProperty("_EnvironmentReflections") && target.GetFloat("_EnvironmentReflections") < 0.5f)
		{
			target.SetFloat("_EnvironmentReflections", 1f);
		}

		if (target.HasProperty("_ReceiveShadows") && target.GetFloat("_ReceiveShadows") < 0.5f)
		{
			target.SetFloat("_ReceiveShadows", 1f);
		}

		var isSpecularWorkflow = target.HasProperty("_WorkflowMode") &&
			UE.Mathf.Approximately(target.GetFloat("_WorkflowMode"), SpecularWorkflowMode);
		var glossMapPropertyName = isSpecularWorkflow ? "_SpecGlossMap" : "_MetallicGlossMap";

		target.SetKeyword("_SPECULAR_SETUP", isSpecularWorkflow);
		target.SetKeyword("_METALLICSPECGLOSSMAP", target.HasTexture(glossMapPropertyName));
		target.SetKeyword("_NORMALMAP", target.HasTexture("_BumpMap"));
		target.SetKeyword("_EMISSION", target.HasEmission());
		target.SetKeyword("_OCCLUSIONMAP", target.HasTexture("_OcclusionMap"));
		target.SetKeyword("_ALPHATEST_ON",
			target.HasProperty("_AlphaClip") && target.GetFloat("_AlphaClip") >= 0.5f);
		target.SetKeyword("_SPECULARHIGHLIGHTS_OFF",
			target.HasProperty("_SpecularHighlights") && UE.Mathf.Approximately(target.GetFloat("_SpecularHighlights"), 0f));
		target.SetKeyword("_ENVIRONMENTREFLECTIONS_OFF",
			target.HasProperty("_EnvironmentReflections") &&
			UE.Mathf.Approximately(target.GetFloat("_EnvironmentReflections"), 0f));
		target.SetKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
			target.HasProperty("_SmoothnessTextureChannel") &&
			UE.Mathf.Approximately(target.GetFloat("_SmoothnessTextureChannel"), SmoothnessTextureChannelAlbedoAlpha) &&
			target.IsOpaqueSurface());
		target.SetKeyword("_SURFACE_TYPE_TRANSPARENT",
			target.HasProperty("_Surface") && UE.Mathf.Approximately(target.GetFloat("_Surface"), TransparentSurfaceType));

		target.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		target.DisableKeyword("_ALPHAMODULATE_ON");
		target.DisableKeyword("_ALPHABLEND_ON");
	}

	public static UE.Material CreateMaterial(in string materialName = "")
	{
		if (CommonShader == null)
		{
			CommonShader = UE.Shader.Find(_commonShaderName);
		}

		var newMaterial = new UE.Material(CommonShader)
		{
			name = materialName,
			hideFlags = UE.HideFlags.DontUnloadUnusedAsset,
			renderQueue = (int)RenderQueue.Geometry
		};

		newMaterial.SetFloat("_Cull", (float)CullMode.Back); // Render face front
		newMaterial.SetFloat("_Blend", 0f);
		newMaterial.SetFloat("_Surface", OpaqueSurfaceType);
		newMaterial.SetFloat("_ZWrite", 1f);
		newMaterial.SetFloat("_AlphaClip", 0f);
		newMaterial.SetFloat("_SrcBlend", (float)BlendMode.One);
		newMaterial.SetFloat("_DstBlend", (float)BlendMode.Zero);
		newMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
		newMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
		newMaterial.SetFloat("_BlendModePreserveSpecular", 1f);
		newMaterial.SetFloat("_QueueOffset", 0f);
		newMaterial.SetFloat("_ReceiveShadows", 1f);
		newMaterial.SetFloat("_SpecularHighlights", 1f);
		newMaterial.SetFloat("_EnvironmentReflections", 1f);
		newMaterial.SetFloat("_SmoothnessTextureChannel", SmoothnessTextureChannelSpecularOrMetallicAlpha);
		newMaterial.SetFloat("_Metallic", 0f);
		newMaterial.SetColor("_BaseColor", UE.Color.white);
		newMaterial.SetColor("_Color", UE.Color.white);
		newMaterial.SetColor("_EmissionColor", UE.Color.black);
		newMaterial.globalIlluminationFlags = UE.MaterialGlobalIlluminationFlags.RealtimeEmissive;
		newMaterial.SetTexture("_MetallicGlossMap", null);
		newMaterial.SetTexture("_SpecGlossMap", null);
		newMaterial.SetTexture("_BumpMap", null);
		newMaterial.SetTexture("_EmissionMap", null);
		newMaterial.SetTexture("_OcclusionMap", null);
		newMaterial.SetTexture("_ParallaxMap", null);
		newMaterial.SetTexture("_DetailMask", null);
		newMaterial.SetTexture("_DetailAlbedoMap", null);
		newMaterial.SetTexture("_DetailNormalMap", null);
		newMaterial.SetFloat("_OcclusionStrength", 1f);
		newMaterial.SetFloat("_BumpScale", 1f);
		newMaterial.SetFloat("_Parallax", 0.005f);
		newMaterial.SetFloat("_DetailAlbedoMapScale", 1f);
		newMaterial.SetFloat("_DetailNormalMapScale", 1f);
		newMaterial.SetFloat("_Smoothness", 0f);
		newMaterial.SetOverrideTag("RenderType", "Opaque");
		newMaterial.UseMetallicWorkflow();

		newMaterial.enableInstancing = true;
		newMaterial.doubleSidedGI = false;
		newMaterial.RefreshLitKeywords();

		return newMaterial;
	}

	public static void SetTransparent(this UE.Material target)
	{
		target.SetOverrideTag("RenderType", "Transparent");
		target.SetFloat("_Surface", TransparentSurfaceType);
		target.SetFloat("_Blend", 0f);

		target.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		target.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		target.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
		target.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
		target.SetFloat("_BlendModePreserveSpecular", 0f);
		target.SetFloat("_AlphaClip", 0f);
		target.SetFloat("_QueueOffset", 0f);
		target.SetFloat("_ZWrite", 0f);

		target.renderQueue = (int)RenderQueue.Transparent;
		target.RefreshLitKeywords();
	}

	public static void SetOpaque(this UE.Material target)
	{
		target.SetOverrideTag("RenderType", "Opaque");
		target.SetFloat("_Surface", OpaqueSurfaceType);
		target.SetFloat("_Blend", 0f);
		target.SetFloat("_SrcBlend", (float)BlendMode.One);
		target.SetFloat("_DstBlend", (float)BlendMode.Zero);
		target.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
		target.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
		target.SetFloat("_BlendModePreserveSpecular", 1f);
		target.SetFloat("_AlphaClip", 0f);
		target.SetFloat("_QueueOffset", 0f);
		target.SetFloat("_ZWrite", 1f);

		target.renderQueue = (int)RenderQueue.Geometry;
		target.RefreshLitKeywords();
	}

	public static void ConvertToSpeedTree(this UE.Material target)
	{
		var existingTexture = target.GetTexture("_BaseMap");
		var existingTextureScale = target.GetTextureScale("_BaseMap");
		// URP Lit stores color in _BaseColor; fall back to _Color for other shaders
		var existingColor = target.HasProperty("_BaseColor") ? target.GetColor("_BaseColor") : target.GetColor("_Color");

		target.shader = SpeedTreeShader;
		target.SetTexture("_MainTex", existingTexture);
		target.SetTextureScale("_MainTex", existingTextureScale);
		target.SetColor("_Color", existingColor);
		target.SetFloat("_Glossiness", 0f);
		target.SetInt("_TwoSided", 0); // 0 = two-sided (no culling), for branch polygons

		target.SetFloat("_BillboardKwToggle", 0);
		target.SetFloat("_BillboardShadowFade", 0f);
		target.DisableKeyword("EFFECT_BILLBOARD");

		// Disable wind for static Gazebo tree models
		target.SetFloat("_WindQuality", 0f);
		target.EnableKeyword("_WINDQUALITY_NONE");
		target.DisableKeyword("_WINDQUALITY_FAST");
		target.DisableKeyword("_WINDQUALITY_BETTER");
		target.DisableKeyword("_WINDQUALITY_BEST");
		target.DisableKeyword("_WINDQUALITY_PALM");

		target.EnableKeyword("_INSTANCING_ON");
	}

	public static void SetBaseColor(this UE.Material target, UE.Color color)
	{
		target.SetColor("_BaseColor", color);
		target.SetColor("_Color", color);

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
		target.SetColor("_EmissionColor", color);
		target.globalIlluminationFlags = UE.MaterialGlobalIlluminationFlags.None;
		target.RefreshLitKeywords();
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
		target.SetTexture("_BumpMap", texture);
		target.RefreshLitKeywords();
	}

	public static void SetSpecular(this UE.Material target, UE.Color color)
	{
		target.UseSpecularWorkflow();
		target.SetFloat("_SmoothnessTextureChannel", SmoothnessTextureChannelSpecularOrMetallicAlpha);
		target.SetTexture("_SpecGlossMap", null);
		target.SetColor("_SpecColor", color);
		target.SetFloat("_Smoothness", color.a);
		target.SetFloat("_SpecularHighlights", 1f);
		target.RefreshLitKeywords();
	}
}