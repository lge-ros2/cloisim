/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using System;
using System.Collections.Generic;

/// <summary>
/// Singleton manager that owns shared Unified Ray Tracing resources
/// (context, acceleration structure, build scratch buffer, scene gathering).
/// Multiple DepthCamera instances share a single BVH instead of each
/// building its own, saving ~80 MB of GPU memory per additional camera.
///
/// Per-camera resources (trace scratch buffer, command buffer, output
/// compute buffers) remain owned by each DepthCamera instance.
/// </summary>
public class URTSensorManager : MonoBehaviour
{
	private static URTSensorManager s_instance;
	private static bool s_applicationQuitting;

	#region "Shared URT resources"

	private RayTracingContext _rtContext;
	private IRayTracingAccelStruct _rtAccelStruct;
	private GraphicsBuffer _rtBuildScratchBuffer;

	/// <summary>Per-instance tracking for transform updates.</summary>
	internal struct InstanceEntry
	{
		public MeshRenderer renderer;
		public int handle;
	}

	private readonly List<InstanceEntry> _rtInstances = new();
	private float _lastSceneGatherTime;
	private const float SceneGatherInterval = 2.0f;
	private int _lastKnownRendererCount;

	/// <summary>Cameras registered for shared BVH access.</summary>
	private readonly HashSet<int> _registeredCameras = new();
	private int _frameOfLastBuild = -1;

	#endregion

	#region "Public API"

	public static URTSensorManager Instance
	{
		get
		{
			if (s_applicationQuitting)
				return null;

			if (s_instance == null)
			{
				s_instance = Main.Core.AddComponent<URTSensorManager>();
			}
			return s_instance;
		}
	}

	/// <summary>The shared RayTracingContext (Compute backend).</summary>
	public RayTracingContext Context => _rtContext;

	/// <summary>The shared acceleration structure. All cameras trace against this.</summary>
	public IRayTracingAccelStruct AccelStruct => _rtAccelStruct;

	/// <summary>
	/// Register a DepthCamera for shared BVH access.
	/// Call from SetupCamera(). Returns false if initialization failed.
	/// </summary>
	public bool Register(int cameraInstanceId)
	{
		if (_rtContext == null)
			Initialize();

		if (_rtAccelStruct == null)
			return false;

		if (_registeredCameras.Add(cameraInstanceId))
		{
			Debug.Log($"[URTSensorManager] Registered camera {cameraInstanceId}, " +
				$"total={_registeredCameras.Count}");
		}

		return true;
	}

	/// <summary>
	/// Unregister a DepthCamera. Call from OnDestroy().
	/// When no cameras remain, shared resources are released.
	/// </summary>
	public void Unregister(int cameraInstanceId)
	{
		_registeredCameras.Remove(cameraInstanceId);

		Debug.Log($"[URTSensorManager] Unregistered camera {cameraInstanceId}, " +
			$"remaining={_registeredCameras.Count}");

		if (_registeredCameras.Count == 0)
		{
			ReleaseResources();
		}
	}

	/// <summary>
	/// Create an <see cref="IRayTracingShader"/> from the shared context.
	/// The shader is lightweight (no GPU allocations) but must be
	/// disposed by the caller.
	/// </summary>
	public IRayTracingShader CreateShader(ComputeShader computeShader)
	{
		return _rtContext?.CreateRayTracingShader(computeShader);
	}

	/// <summary>
	/// Ensure the scene BVH is up-to-date for this frame.
	/// Called by each DepthCamera at the start of ExecuteRender.
	/// Only the first caller per frame does actual work; subsequent
	/// calls in the same frame are no-ops.
	/// </summary>
	/// <param name="cmd">CommandBuffer to record the Build into.</param>
	public void EnsureBVHReady(CommandBuffer cmd)
	{
		if (_rtAccelStruct == null)
			return;

		var currentFrame = Time.frameCount;
		if (_frameOfLastBuild == currentFrame)
			return; // Already built this frame

		_frameOfLastBuild = currentFrame;

		// --- Detect new/removed objects ---
		var currentRendererCount = Resources.FindObjectsOfTypeAll<MeshRenderer>().Length;
		var sceneChanged = currentRendererCount != _lastKnownRendererCount;
		var realtimeNow = Time.realtimeSinceStartup;

		if (sceneChanged || (realtimeNow - _lastSceneGatherTime > SceneGatherInterval))
		{
			GatherSceneMeshes();
			_lastKnownRendererCount = currentRendererCount;
		}

		// --- Update transforms ---
		UpdateInstanceTransforms();

		// --- Resize scratch buffer if needed ---
		RayTracingHelper.ResizeScratchBufferForBuild(_rtAccelStruct, ref _rtBuildScratchBuffer);

		// --- Build BVH (idempotent if already built) ---
		_rtAccelStruct.Build(cmd, _rtBuildScratchBuffer);
	}

	#endregion

	#region "Initialization / Teardown"

	private void Initialize()
	{
		var resources = new RayTracingResources();
		if (!resources.LoadFromRenderPipelineResources())
		{
			Debug.LogError("[URTSensorManager] Failed to load RayTracingResources from render pipeline");
			return;
		}

		_rtContext = new RayTracingContext(RayTracingBackend.Compute, resources);

		_rtAccelStruct = _rtContext.CreateAccelerationStructure(
			new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastBuild });

		Debug.Log("[URTSensorManager] Initialized (Compute backend)");
	}

	private void ReleaseResources()
	{
		_rtBuildScratchBuffer?.Dispose();
		_rtBuildScratchBuffer = null;

		_rtAccelStruct?.Dispose();
		_rtAccelStruct = null;

		_rtContext?.Dispose();
		_rtContext = null;

		_rtInstances.Clear();
		_lastSceneGatherTime = 0f;
		_lastKnownRendererCount = 0;
		_frameOfLastBuild = -1;

		Debug.Log("[URTSensorManager] Released shared URT resources");
	}

	#endregion

	#region "Scene Gathering"

	/// <summary>
	/// Gather all active MeshRenderers in the scene that match the
	/// Default+Plane culling mask and populate the shared accel structure.
	/// </summary>
	private void GatherSceneMeshes()
	{
		if (_rtAccelStruct == null)
			return;

		_rtAccelStruct.ClearInstances();
		_rtInstances.Clear();

		var cullingMask = LayerMask.GetMask("Default", "Plane");
		var renderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();

		int addedCount = 0;
		int skippedLayer = 0;
		int skippedNoMesh = 0;
		int skippedError = 0;

		foreach (var renderer in renderers)
		{
			if (!renderer.gameObject.scene.isLoaded)
				continue;

			if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
				continue;

			if ((cullingMask & (1 << renderer.gameObject.layer)) == 0)
			{
				skippedLayer++;
				continue;
			}

			var meshFilter = renderer.GetComponent<MeshFilter>();
			if (meshFilter == null || meshFilter.sharedMesh == null)
			{
				skippedNoMesh++;
				continue;
			}

			var mesh = meshFilter.sharedMesh;

			for (int sub = 0; sub < mesh.subMeshCount; sub++)
			{
				try
				{
					var desc = new MeshInstanceDesc(mesh, sub);
					desc.localToWorldMatrix = renderer.localToWorldMatrix;
					desc.mask = 0xFF;
					desc.enableTriangleCulling = false;
					desc.opaqueGeometry = true;

					var handle = _rtAccelStruct.AddInstance(desc);
					_rtInstances.Add(new InstanceEntry
					{
						renderer = renderer,
						handle = handle
					});
					addedCount++;
				}
				catch (Exception e)
				{
					skippedError++;
					Debug.LogWarning($"[URTSensorManager] Failed to add '{renderer.name}' sub={sub} " +
						$"mesh='{mesh.name}' verts={mesh.vertexCount}: {e.Message}");
				}
			}
		}

		_lastSceneGatherTime = Time.realtimeSinceStartup;

		Debug.Log($"[URTSensorManager] GatherSceneMeshes: added={addedCount}, " +
			$"skippedLayer={skippedLayer}, skippedNoMesh={skippedNoMesh}, " +
			$"skippedError={skippedError}, totalRenderers={renderers.Length}");

		// Allocate / resize the build scratch buffer
		RayTracingHelper.ResizeScratchBufferForBuild(_rtAccelStruct, ref _rtBuildScratchBuffer);
	}

	/// <summary>
	/// Update world-space transforms for all cached instances.
	/// </summary>
	private void UpdateInstanceTransforms()
	{
		bool needsRebuild = false;

		for (int i = _rtInstances.Count - 1; i >= 0; i--)
		{
			var entry = _rtInstances[i];
			if (entry.renderer == null || !entry.renderer.gameObject.activeInHierarchy)
			{
				needsRebuild = true;
				continue;
			}

			_rtAccelStruct.UpdateInstanceTransform(entry.handle, entry.renderer.localToWorldMatrix);
		}

		if (needsRebuild)
		{
			_lastSceneGatherTime = 0f; // Force re-gather next call
		}
	}

	#endregion

	#region "Unity lifecycle"

	private void OnApplicationQuit()
	{
		s_applicationQuitting = true;
	}

	private void OnDestroy()
	{
		ReleaseResources();
		_registeredCameras.Clear();

		if (s_instance == this)
			s_instance = null;
	}

	#endregion
}
