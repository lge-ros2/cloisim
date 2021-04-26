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

	public static Material GetNewMaterial(in string name = "")
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

		newMaterial.name = name;
		newMaterial.enableInstancing = true;
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

	public static Color GetColor(in SDF.Color value)
	{
		return new Color((float)value.R, (float)value.G, (float)value.B, (float)value.A);
	}

	public static Vector3 GetScalar(in double x, in double y, in double z)
	{
		return new Vector3(Mathf.Abs((float)y), Mathf.Abs((float)z), Mathf.Abs((float)x));
	}

	public static Vector3 GetPosition(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 GetPosition(in SDF.Vector3<double> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Vector3 GetPosition(in SDF.Vector3<int> value)
	{
		return (value == null) ? Vector3.zero : GetPosition(value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in SDF.Vector3<double> value)
	{
		return GetRotation(new SDF.Quaternion<double>(value.X, value.Y, value.Z));
	}

	public static Quaternion GetRotation(in SDF.Quaternion<double> value)
	{
		return (value == null) ? Quaternion.identity : GetRotation(value.W, value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in double w, in double x, in double y, in double z)
	{
		return new Quaternion((float)y, (float)-z, (float)-x, (float)w);
	}

	public static Vector3 GetScale(in SDF.Vector3<double> value)
	{
		var scaleVector = GetPosition(value);
		scaleVector.x = Mathf.Abs(scaleVector.x);
		scaleVector.y = Mathf.Abs(scaleVector.y);
		scaleVector.z = Mathf.Abs(scaleVector.z);
		return scaleVector;
	}

	public static Vector3 GetNormal(in SDF.Vector3<int> value)
	{
		return GetPosition(value);
	}

	public static Vector3 GetAxis(SDF.Vector3<int> axis)
	{
		return GetPosition(axis);
	}

	public static Vector3 GetDirection(SDF.Vector3<double> direction)
	{
		return GetPosition(direction);
	}

	public static bool IsTopModel(in GameObject targetObject)
	{
		return IsTopModel(targetObject.transform);
	}

	public static bool IsTopModel(in Transform targetTransform)
	{
		return targetTransform.parent.Equals(targetTransform.root);
	}
}