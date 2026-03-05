/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor.Rendering.CLOiSim
{
	/// <summary>
	/// Auto-detects whether RayTracingRenderPipelineResources is publicly
	/// accessible in the current Unity/URP version, and sets an editor-only
	/// scripting define so that the proper IRenderPipelineGraphicsSettingsStripper
	/// can be compiled when the type is available.
	///
	/// When the type is internal (some Unity 6 builds), the stripper cannot be
	/// compiled, so we rely on:
	///   1. link.xml (Assets/link.xml) to preserve the URT assembly from IL stripping.
	///   2. URTSensorManager.TryLoadResourcesViaReflection() to load compute shaders
	///      at runtime without compile-time dependency on the internal type.
	/// </summary>
	[InitializeOnLoad]
	static class URTTypeAccessibilityDetector
	{
		private const string Define = "CLOISIM_URT_STRIPPER_AVAILABLE";
		private const string TypeFullName = "UnityEngine.Rendering.UnifiedRayTracing.RayTracingRenderPipelineResources";

		static URTTypeAccessibilityDetector()
		{
			try
			{
				// Search across all loaded assemblies — the type may live in a
				// different assembly than RayTracingResources (e.g., URP-specific
				// assembly vs Core RP assembly).
				System.Type rtResType = null;
				foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
				{
					rtResType = asm.GetType(TypeFullName, throwOnError: false);
					if (rtResType != null) break;
				}

				var isPublic = rtResType != null && rtResType.IsPublic;
				Debug.Log($"[URTTypeAccessibilityDetector] {TypeFullName}: " +
					(rtResType == null ? "NOT FOUND" : (isPublic ? "public" : "internal")) +
					(rtResType != null ? $" (assembly: {rtResType.Assembly.GetName().Name})" : ""));

				var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
				if (buildTarget == BuildTargetGroup.Unknown)
					buildTarget = BuildTargetGroup.Standalone;

				var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTarget);
				PlayerSettings.GetScriptingDefineSymbols(namedTarget, out var defines);

				var hasDefine = System.Array.Exists(defines, d => d == Define);

				if (isPublic && !hasDefine)
				{
					var newDefines = new string[defines.Length + 1];
					defines.CopyTo(newDefines, 0);
					newDefines[defines.Length] = Define;
					PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
					Debug.Log($"[URTTypeAccessibilityDetector] Added {Define} scripting define");
				}
				else if (!isPublic && hasDefine)
				{
					var newDefines = System.Array.FindAll(defines, d => d != Define);
					PlayerSettings.SetScriptingDefineSymbols(namedTarget, newDefines);
					Debug.Log($"[URTTypeAccessibilityDetector] Removed {Define} scripting define");
				}
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[URTTypeAccessibilityDetector] Failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
			}
		}
	}

#if CLOISIM_URT_STRIPPER_AVAILABLE
	/// <summary>
	/// Prevents Unity from stripping RayTracingRenderPipelineResources during
	/// player builds. Only compiled when the type is publicly accessible
	/// (detected by URTTypeAccessibilityDetector above).
	/// </summary>
	class URTResourcesStripper
		: IRenderPipelineGraphicsSettingsStripper<
			UnityEngine.Rendering.UnifiedRayTracing.RayTracingRenderPipelineResources>
	{
		public bool active => true;

		public bool CanRemoveSettings(
			UnityEngine.Rendering.UnifiedRayTracing.RayTracingRenderPipelineResources settings) => false;
	}
#endif
}
