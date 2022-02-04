/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

public partial class SDF2Unity
{
	private static readonly string commonShaderName = "Universal Render Pipeline/Simple Lit";
	private static readonly string speedTreeShaderName = "Universal Render Pipeline/Nature/SpeedTree8";
	public static Shader CommonShader = Shader.Find(commonShaderName);
	public static Shader SpeedTreeShader = Shader.Find(speedTreeShaderName);

	public static Material GetNewMaterial(in string materialName = "")
	{
		var newMaterial = new Material(SDF2Unity.CommonShader);
		newMaterial.SetFloat("_WorkflowMode", 0); // set to specular mode
		newMaterial.SetFloat("_Cull", (float)CullMode.Back); // Render face front
		newMaterial.SetFloat("_ZWrite", 1);
		newMaterial.SetFloat("_SpecularHighlights", 1f);
		newMaterial.SetFloat("_Smoothness", 0.0f);
		newMaterial.SetFloat("_SmoothnessTextureChannel", 1f);
		newMaterial.SetFloat("_EnvironmentReflections", 1f);
		newMaterial.SetFloat("_GlossyReflections", 0f);
		newMaterial.SetFloat("_Glossiness", 0f);
		newMaterial.SetFloat("_GlossMapScale", 0f);
		newMaterial.SetFloat("_ReceiveShadows", 1);
		newMaterial.EnableKeyword("_SPECGLOSSMAP");
		newMaterial.EnableKeyword("_SPECULAR_SETUP");
		newMaterial.EnableKeyword("_EMISSION");
		newMaterial.EnableKeyword("_NORMALMAP");
		newMaterial.EnableKeyword("_INSTANCING_ON");
		newMaterial.EnableKeyword("_DOTS_INSTANCING_ON");

		newMaterial.name = materialName;
		newMaterial.enableInstancing = true;
		newMaterial.doubleSidedGI = false;
		newMaterial.renderQueue = (int)RenderQueue.Background;
		// newMaterial.hideFlags |= HideFlags.NotEditable;

		return newMaterial;
	}

	public static void SetMaterialTransparent(Material targetMaterial)
	{
		targetMaterial.SetOverrideTag("RenderType", "Transparent");
		targetMaterial.SetFloat("_Surface", 1); // set to transparent
		targetMaterial.SetFloat("_Mode", 3); // set to transparent Mode
		targetMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		targetMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		targetMaterial.SetFloat("_AlphaClip", 0);
		targetMaterial.SetFloat("_QueueOffset", 1);
		targetMaterial.DisableKeyword("_ALPHATEST_ON");
		targetMaterial.DisableKeyword("_ALPHABLEND_ON");
		targetMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
		targetMaterial.renderQueue = (int)RenderQueue.Transparent;
	}

	public static void SetMaterialOpaque(Material targetMaterial)
	{
		targetMaterial.SetOverrideTag("RenderType", "Opaque");
		targetMaterial.SetFloat("_Surface", 0); // set to opaque
		targetMaterial.SetFloat("_Mode", 0); // set to opaque Mode
		targetMaterial.DisableKeyword("_ALPHATEST_ON");
		targetMaterial.DisableKeyword("_ALPHABLEND_ON");
		targetMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		targetMaterial.renderQueue = (int)RenderQueue.Geometry;
	}

	public static void SetMaterialSpeedTree(Material targetMaterial)
	{
		var existingTexture = targetMaterial.GetTexture("_BaseMap");
		var existingColor = targetMaterial.GetColor("_Color");
		existingColor.a = 0.55f;
		targetMaterial.shader = SpeedTreeShader;
		targetMaterial.EnableKeyword("EFFECT_BILLBOARD");
		targetMaterial.EnableKeyword("_INSTANCING_ON");
		targetMaterial.SetTexture("_MainTex", existingTexture);
		targetMaterial.SetColor("_Color", existingColor);
		targetMaterial.SetFloat("_Glossiness", 0f);
		targetMaterial.SetInt("_TwoSided", 2);
		targetMaterial.SetFloat("_BillboardKwToggle", 1f);
		targetMaterial.SetFloat("_BillSboardShadowFade", 0f);
	}

	public static Mesh MergeMeshes(in MeshFilter[] meshFilters)
	{
		var combine = new CombineInstance[meshFilters.Length];
		var totalVertexCount = 0;
		for (var combineIndex = 0; combineIndex < meshFilters.Length; combineIndex++)
		{
			var meshFilter = meshFilters[combineIndex];
			combine[combineIndex].mesh = meshFilter.sharedMesh;
			totalVertexCount += meshFilter.sharedMesh.vertexCount;
			combine[combineIndex].transform = meshFilter.transform.localToWorldMatrix;
			// Debug.LogFormat("{0}, {1}: {2}", meshFilter.name, meshFilter.transform.name, combine[combineIndex].transform);
		}

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, true, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}

	public static Mesh MergeMeshes(in MeshCollider[] meshColliders, in Matrix4x4 geometryWorldToLocalMatrix)
	{
		var combine = new CombineInstance[meshColliders.Length];
		var totalVertexCount = 0;
		for (var index = 0; index < meshColliders.Length; index++)
		{
			var meshCollider = meshColliders[index];
			combine[index].mesh = meshCollider.sharedMesh;
			totalVertexCount += combine[index].mesh.vertexCount;
			var meshColliderTransform = meshCollider.transform;
			combine[index].transform = geometryWorldToLocalMatrix * meshColliderTransform.localToWorldMatrix;
		}

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, false, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}
}