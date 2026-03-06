/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SensorDevices
{
	/// <summary>
	/// Holds explicit references to the 9 Unified Ray Tracing compute shaders
	/// from the SRP Core package (RadeonRays BVH + common utilities).
	///
	/// Because RayTracingRenderPipelineResources is internal in some Unity 6
	/// builds, it can get stripped from player builds even when serialized in
	/// the URP global settings. This asset provides a reliable fallback:
	/// placed in a Resources folder, it guarantees the shaders are included
	/// in the build and can be loaded at runtime.
	///
	/// The asset is auto-populated by URTComputeShaderRefsUpdater (Editor script)
	/// on every domain reload.
	/// </summary>
	public class URTComputeShaderRefs : ScriptableObject
	{
		private const string ResourcePath = "URT/URTComputeShaderRefs";

		[Header("Common (both Hardware and Compute backends)")]
		public ComputeShader geometryPoolKernels;
		public ComputeShader copyBuffer;

		[Header("RadeonRays BVH (Compute backend only)")]
		public ComputeShader copyPositions;
		public ComputeShader bitHistogram;
		public ComputeShader blockReducePart;
		public ComputeShader blockScan;
		public ComputeShader buildHlbvh;
		public ComputeShader restructureBvh;
		public ComputeShader scatter;

		/// <summary>
		/// Returns true if all 9 shader references are assigned.
		/// </summary>
		public bool IsValid =>
			geometryPoolKernels != null &&
			copyBuffer != null &&
			copyPositions != null &&
			bitHistogram != null &&
			blockReducePart != null &&
			blockScan != null &&
			buildHlbvh != null &&
			restructureBvh != null &&
			scatter != null;

		/// <summary>
		/// Load the shader refs asset from Resources at runtime.
		/// Returns null if the asset is missing or invalid.
		/// </summary>
		public static URTComputeShaderRefs Load()
		{
			var refs = Resources.Load<URTComputeShaderRefs>(ResourcePath);
			if (refs != null && refs.IsValid)
				return refs;
			return null;
		}
	}
}
