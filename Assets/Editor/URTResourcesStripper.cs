/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;

namespace UnityEditor.Rendering.CLOiSim
{
	/// <summary>
	/// Prevents Unity from stripping RayTracingRenderPipelineResources during
	/// player builds. Without this, the Unified Ray Tracing compute backend
	/// cannot initialise because RayTracingResources.LoadFromRenderPipelineResources()
	/// fails (the resource type is stripped by default).
	/// </summary>
	class URTResourcesStripper : IRenderPipelineGraphicsSettingsStripper<RayTracingRenderPipelineResources>
	{
		public bool active => true;
		public bool CanRemoveSettings(RayTracingRenderPipelineResources settings) => false;
	}
}
