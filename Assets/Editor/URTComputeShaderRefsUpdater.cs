/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEditor;

namespace SensorDevices.Editor
{
	/// <summary>
	/// Auto-creates and populates the URTComputeShaderRefs asset in
	/// Assets/Resources/URT/ on every Editor domain reload.
	///
	/// The 9 compute shaders are loaded from the SRP Core package
	/// (com.unity.render-pipelines.core) using AssetDatabase.
	/// </summary>
	[InitializeOnLoad]
	static class URTComputeShaderRefsUpdater
	{
		private const string AssetDir = "Assets/Resources/URT";
		private const string AssetPath = AssetDir + "/URTComputeShaderRefs.asset";
		private const string PackageBase = "Packages/com.unity.render-pipelines.core/Runtime/UnifiedRayTracing/";

		static URTComputeShaderRefsUpdater()
		{
			// In batchmode, delayCall may not fire; run directly.
			if (Application.isBatchMode)
				EnsureAsset();
			else
				EditorApplication.delayCall += EnsureAsset;
		}

		/// <summary>
		/// Menu item to manually recreate/update the asset.
		/// </summary>
		[MenuItem("CLOiSim/Update URT Compute Shader Refs")]
		public static void EnsureAsset()
		{
			// Ensure the directory exists
			if (!AssetDatabase.IsValidFolder("Assets/Resources"))
				AssetDatabase.CreateFolder("Assets", "Resources");
			if (!AssetDatabase.IsValidFolder(AssetDir))
				AssetDatabase.CreateFolder("Assets/Resources", "URT");

			// Load or create the asset
			var refs = AssetDatabase.LoadAssetAtPath<URTComputeShaderRefs>(AssetPath);
			if (refs == null)
			{
				refs = ScriptableObject.CreateInstance<URTComputeShaderRefs>();
				AssetDatabase.CreateAsset(refs, AssetPath);
				Debug.Log("[URTComputeShaderRefsUpdater] Created URTComputeShaderRefs asset");
			}

			// Load all 9 shaders from the SRP Core package
			refs.geometryPoolKernels = LoadShader("Common/GeometryPool/GeometryPoolKernels.compute");
			refs.copyBuffer          = LoadShader("Common/Utilities/CopyBuffer.compute");
			refs.copyPositions       = LoadShader("Compute/RadeonRays/kernels/copyPositions.compute");
			refs.bitHistogram        = LoadShader("Compute/RadeonRays/kernels/bit_histogram.compute");
			refs.blockReducePart     = LoadShader("Compute/RadeonRays/kernels/block_reduce_part.compute");
			refs.blockScan           = LoadShader("Compute/RadeonRays/kernels/block_scan.compute");
			refs.buildHlbvh          = LoadShader("Compute/RadeonRays/kernels/build_hlbvh.compute");
			refs.restructureBvh      = LoadShader("Compute/RadeonRays/kernels/restructure_bvh.compute");
			refs.scatter             = LoadShader("Compute/RadeonRays/kernels/scatter.compute");

			EditorUtility.SetDirty(refs);
			AssetDatabase.SaveAssets();

			if (refs.IsValid)
				Debug.Log("[URTComputeShaderRefsUpdater] All 9 URT compute shaders assigned");
			else
				Debug.LogWarning("[URTComputeShaderRefsUpdater] Some shaders could not be found — URT Compute backend may not work");
		}

		static ComputeShader LoadShader(string relativePath)
		{
			var fullPath = PackageBase + relativePath;
			var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(fullPath);
			if (shader == null)
				Debug.LogWarning($"[URTComputeShaderRefsUpdater] Shader not found: {fullPath}");
			return shader;
		}
	}
}
