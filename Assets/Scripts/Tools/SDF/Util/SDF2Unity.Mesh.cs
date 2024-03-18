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
	private static readonly string commonShaderName = "Universal Render Pipeline/Simple Lit";
	private static readonly string speedTreeShaderName = "Universal Render Pipeline/Nature/SpeedTree8";
	public static Shader CommonShader = Shader.Find(commonShaderName);
	public static Shader SpeedTreeShader = Shader.Find(speedTreeShaderName);

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

		public static void SetSpeedTree(UE.Material target)
		{
			var existingTexture = target.GetTexture("_BaseMap");
			var existingColor = target.GetColor("_BaseColor");
			existingColor.a = 0.55f;
			target.shader = SpeedTreeShader;
			target.SetTexture("_BaseMap", existingTexture);
			target.SetColor("_BaseColor", existingColor);
			target.SetFloat("_Smoothness", 0f);
			target.SetInt("_TwoSided", 2);
			target.SetFloat("_BillboardKwToggle", 1f);
			target.SetFloat("_BillSboardShadowFade", 0f);
			target.EnableKeyword("EFFECT_BILLBOARD");
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