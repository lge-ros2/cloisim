/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;

public partial class SDF2Unity
{
	private static readonly string commonShaderName = "Universal Render Pipeline/Lit";
	public static Shader CommonShader = Shader.Find(commonShaderName);

	public static Material GetNewMaterial(in string materialName = "")
	{
		var defaultEmissionColor = Color.white - Color.black;
		var newMaterial = new Material(SDF2Unity.CommonShader);
		newMaterial.SetFloat("_WorkflowMode", 0); // set to specular mode
		// newMaterial.SetFloat("_Surface", 1); // set to transparent
		// newMaterial.SetOverrideTag("RenderType", "Transparent");
		newMaterial.SetFloat("_Cull", (float)CullMode.Back); // Render face front
		// newMaterial.SetFloat("_Mode", 3); // set to transparent Mode
		// newMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
		// newMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
		newMaterial.SetFloat("_ZWrite", 1);
		newMaterial.SetFloat("_SpecularHighlights", 1f);
		newMaterial.SetFloat("_Smoothness", 0.5f);
		newMaterial.SetFloat("_SmoothnessTextureChannel", 1f);
		newMaterial.SetFloat("_EnvironmentReflections", 1f);
		newMaterial.SetFloat("_GlossyReflections", 0f);
		newMaterial.SetFloat("_Glossiness", 0f);
		newMaterial.SetFloat("_GlossMapScale", 0f);
		newMaterial.SetFloat("_ReceiveShadows", 1);
		// newMaterial.DisableKeyword("_ALPHATEST_ON");
		// newMaterial.DisableKeyword("_ALPHABLEND_ON");
		// newMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
		newMaterial.EnableKeyword("_SPECGLOSSMAP");
		newMaterial.EnableKeyword("_SPECULAR_SETUP");
		newMaterial.EnableKeyword("_EMISSION");
		newMaterial.DisableKeyword("_NORMALMAP");

		newMaterial.name = materialName;
		newMaterial.enableInstancing = true;
		newMaterial.doubleSidedGI = false;
		// newMaterial.renderQueue = (int)RenderQueue.Transparent;
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
		targetMaterial.DisableKeyword("_ALPHATEST_ON");
		targetMaterial.DisableKeyword("_ALPHABLEND_ON");
		targetMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
		targetMaterial.renderQueue = (int)RenderQueue.Transparent;
		// targetMaterial.SetFloat("_QueueOffset", 20);
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

	public static Mesh MergeMeshes(in MeshFilter[] meshFilters)
	{
		var combine = new CombineInstance[meshFilters.Length];
		var totalVertexCount = 0;
		for (var combineIndex = 0; combineIndex < meshFilters.Length; combineIndex++)
		{
			var meshFilter = meshFilters[combineIndex];
			combine[combineIndex].mesh = meshFilter.sharedMesh;
			totalVertexCount += meshFilter.sharedMesh.vertexCount;
			combine[combineIndex].transform = meshFilters[combineIndex].transform.localToWorldMatrix;
			// Debug.LogFormat("{0}, {1}: {2}", meshFilter.name, meshFilter.transform.name, combine[combineIndex].transform);
		}

		var newCombinedMesh = new Mesh();
		newCombinedMesh.indexFormat = (totalVertexCount >= System.UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, false, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
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
		newCombinedMesh.indexFormat = (totalVertexCount >= System.UInt16.MaxValue) ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, false, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}
}