/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using Unity.Profiling;
using System;
using System.Collections.Generic;

public static class RayTracingResourcesExtension
{
	public static void LoadFromURTResourcesByManual(this RayTracingResources resources)
	{
		var shaderRefs = Resources.Load<URTShaderReferences>("Shader/URTShaderReferences");
		if (shaderRefs == null)
		{
			Debug.LogError("[URT] Failed to load URTShaderReferences asset from Resources. Make sure the asset exists and is named 'URTShaderReferences' inside a Resources folder.");
			return;
		}

		SetShader(resources, "geometryPoolKernels", shaderRefs.geometryPoolKernels);
		SetShader(resources, "copyBuffer", shaderRefs.copyBuffer);
		SetShader(resources, "copyPositions", shaderRefs.copyPositions);
		SetShader(resources, "bitHistogram", shaderRefs.bitHistogram);
		SetShader(resources, "blockReducePart", shaderRefs.blockReducePart);
		SetShader(resources, "blockScan", shaderRefs.blockScan);
		SetShader(resources, "buildHlbvh", shaderRefs.buildHlbvh);
		SetShader(resources, "restructureBvh", shaderRefs.restructureBvh);
		SetShader(resources, "scatter", shaderRefs.scatter);
	}

	private static void SetShader(RayTracingResources resources, string name, ComputeShader shader)
	{
		if (shader == null)
		{
			Debug.LogError($"[URT] ComputeShader for '{name}' is null. Check the URTShaderReferences asset in the editor.");
			return;
		}

		var type = typeof(RayTracingResources);
		string pascalName = char.ToUpper(name[0]) + name.Substring(1);

		// Search fields (including C# compiler-generated backing fields)
		string[] possibleFields = {
			name, pascalName,
			"m_" + pascalName, "m_" + name,
			$"<{name}>k__BackingField", $"<{pascalName}>k__BackingField"
		};

		foreach (var pName in possibleFields)
		{
			var field = type.GetField(pName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			if (field != null)
			{
				field.SetValue(resources, shader);
				return;
			}
		}

		// Search properties
		string[] possibleProps = { name, pascalName };
		foreach (var pName in possibleProps)
		{
			var prop = type.GetProperty(pName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(resources, shader);
				return;
			}
		}

		Debug.LogWarning($"[URT] Could not find field or property for '{name}' on RayTracingResources via Reflection. It might have been stripped by IL2CPP.");
	}
}

/// <summary>
/// Singleton manager that owns shared Unified Ray Tracing resources
/// (context, acceleration structure, build scratch buffer, scene gathering).
/// Multiple DepthCamera instances share a single BVH instead of each
/// building its own, saving ~80 MB of GPU memory per additional camera.
///
/// Per-camera resources (trace scratch buffer, command buffer, output
/// compute buffers) remain owned by each DepthCamera instance.
/// </summary>
// Execution order is forced LATE so this component's LateUpdate runs *after*
// SensorRenderManager.LateUpdate (default order 0), which is where every URT
// sensor records and submits its trace dispatch for the frame. The build fence
// (see IsPriorTraceConsumed) is created in our LateUpdate and must capture
// those already-submitted dispatches, so we must run after them.
[DefaultExecutionOrder(1000)]
public class URTSensorManager : MonoBehaviour
{
	private static URTSensorManager s_instance;
	private static bool s_applicationQuitting = false;

	#region "Profiling markers"
	private static readonly ProfilerMarker s_EnsureBVHReadyMarker = new("URTSensorManager.EnsureBVHReady");
	private static readonly ProfilerMarker s_GatherSceneMeshesMarker = new("URTSensorManager.GatherSceneMeshes");
	private static readonly ProfilerMarker s_UpdateInstanceTransformsMarker = new("URTSensorManager.UpdateInstanceTransforms");
	private static readonly ProfilerMarker s_BuildBVHMarker = new("URTSensorManager.BuildBVH");
	#endregion

	#region "Diagnostics"

	private const int DiagRingSize = 16;
	private readonly Queue<string> _diagHistory = new(DiagRingSize + 1);
	private int _diagPrevInstanceCount = -1;
	private int _diagPrevScratchHash;

	private void DiagRecord(string entry)
	{
		_diagHistory.Enqueue(entry);
		if (_diagHistory.Count > DiagRingSize)
			_diagHistory.Dequeue();
	}

	/// <summary>
	/// Dump the last DiagRingSize URT state-change events to the log.
	/// Called automatically when a GPU UAV error is detected.
	/// </summary>
	public static void DumpDiagHistory(string trigger)
	{
		var inst = s_instance;
		if (inst == null)
		{
			Debug.LogError("[URT-DIAG] DumpDiagHistory: URTSensorManager instance is null.");
			return;
		}

		var sb = new System.Text.StringBuilder();
		sb.AppendLine($"[URT-DIAG] === History dump triggered by: {trigger} (frame={Time.frameCount}) ===");
		foreach (var line in inst._diagHistory)
			sb.AppendLine(line);
		sb.AppendLine("[URT-DIAG] === End of history ===");
		Debug.LogError(sb.ToString());
	}

	#endregion

	#region "Shared URT resources"

	private RayTracingContext _rtContext;
	private IRayTracingAccelStruct _rtAccelStruct;
	private GraphicsBuffer _rtBuildScratchBuffer;

	#region "Safe scratch-buffer management"
	// The package's RayTracingHelper.Resize*ScratchBuffer* disposes the old
	// GraphicsBuffer immediately. Because the shared build scratch buffer (and
	// the per-camera trace scratch buffers) are referenced by Build()/Dispatch()
	// commands that may still be executing on the GPU, an immediate dispose on
	// the next frame's growth corrupts the in-flight command stream
	// ("missing UAV ID ... incompatible ComputeBuffer" -> device-lost -> freeze).
	//
	// We replace it with: grow-with-headroom (never shrink, so reallocations are
	// rare) + deferred dispose. The old buffer is freed only once a GraphicsFence
	// captured at deferral time has passed — i.e. the GPU has actually finished the
	// dispatches that referenced it. A fixed frame-count delay is NOT enough: under
	// a heavy spike the GPU can lag many frames behind the main thread, and freeing
	// a still-in-flight buffer corrupts the dispatch ("missing UAV ID") which can
	// hang the GPU context (NVIDIA Xid 109 CTX SWITCH TIMEOUT) -> freeze. The
	// frame-count path remains only as a fallback when GraphicsFence is unsupported.

	/// <summary>Extra capacity multiplier applied when (re)allocating a scratch buffer.</summary>
	private const float ScratchHeadroomFactor = 2.0f;

	/// <summary>
	/// Fallback only (GraphicsFence unsupported): frames an old scratch buffer is kept
	/// alive before disposal. Must exceed the max GPU/readback pipeline lag.
	/// </summary>
	private const int ScratchSafetyFrames = 6;

	private readonly Queue<(GraphicsBuffer buffer, GraphicsFence fence, bool hasFence, int frame)> _deferredScratchFree = new();
	#endregion

	/// <summary>Per-instance tracking for transform updates.</summary>
	internal struct InstanceEntry
	{
		public MeshRenderer renderer;
		public Mesh mesh;
		public int subMeshIndex;
		public int handle;
	}

	private readonly struct InstanceKey : IEquatable<InstanceKey>
	{
		private readonly EntityId _rendererId;
		private readonly EntityId _meshId;
		private readonly int _subMeshIndex;

		public InstanceKey(MeshRenderer renderer, Mesh mesh, int subMeshIndex)
		{
			_rendererId = renderer != null ? renderer.GetEntityId() : default;
			_meshId = mesh != null ? mesh.GetEntityId() : default;
			_subMeshIndex = subMeshIndex;
		}

		public bool Equals(InstanceKey other)
		{
			return _rendererId.Equals(other._rendererId) &&
				_meshId.Equals(other._meshId) &&
				_subMeshIndex == other._subMeshIndex;
		}

		public override bool Equals(object obj)
		{
			return obj is InstanceKey other && Equals(other);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(_rendererId, _meshId, _subMeshIndex);
		}
	}

	private readonly List<InstanceEntry> _rtInstances = new();
	private readonly HashSet<InstanceKey> _desiredInstanceKeys = new();
	private readonly HashSet<InstanceKey> _existingInstanceKeys = new();

	private const float MinSceneGatherInterval = 10.0f;
	private const float RendererCountScalingFactor = 500.0f;

	[SerializeField]
	private float _sceneGatherInterval = MinSceneGatherInterval;
	[SerializeField]
	private float _lastSceneGatherTime;

	private int _cullingMask;

	/// <summary>
	/// Dirty flag set by OnEnable/OnDisable callbacks on MeshRenderers.
	/// Avoids per-frame FindObjectsByType allocation.
	/// </summary>
	private bool _sceneDirty = true;

	/// <summary>Cameras registered for shared BVH access.</summary>
	private readonly HashSet<EntityId> _registeredCameras = new();

	[SerializeField]
	private int _frameOfLastBuild = -1;

	#region "In-flight TLAS protection"
	// The package's ComputeRayTracingAccelStruct disposes its top-level accel
	// structure buffers (instanceInfos / topLevelBvh) IMMEDIATELY on any
	// AddInstance / RemoveInstance / UpdateInstanceTransform (via
	// FreeTopLevelAccelStruct). Those exact buffers are bound as trace-dispatch
	// INPUTS (RadeonRays Bind: "instanceInfos"/"bvh"/...). If we mutate the accel
	// struct while a previous frame's trace dispatch still reads them on the GPU,
	// the binding resolves to a freed buffer -> "missing input compute buffer ID
	// / incompatible ComputeBuffer" -> Vulkan device-lost -> freeze.
	//
	// Unlike the build scratch buffer, we cannot defer-free these (the package
	// owns and disposes them). Instead we defer the *mutation*: a GraphicsFence is
	// recorded after each build cycle's dispatches, and the next cycle's
	// gather/transform-update/build is skipped until that fence has passed (the
	// GPU has consumed the dispatches that read the old TLAS). When skipped, the
	// still-valid TLAS is simply re-traced; the only cost is BVH transforms being
	// 1-2 frames stale, which is imperceptible for sensors.

	private static readonly bool s_graphicsFenceSupported = SystemInfo.supportsGraphicsFence;
	private GraphicsFence _buildFence;
	private bool _hasBuildFence;
	private bool _pendingFenceNeeded;
	private bool _hasBuiltOnce;
	// Frame of the most recent URT render (= most recent trace dispatch submission).
	// Used only by the no-fence fallback to require a safe frame gap before mutating.
	private int _frameOfLastRenderSubmit = -1;
	#endregion

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
				s_instance._cullingMask = LayerMask.GetMask("Default", "Plane");
			}
			return s_instance;
		}
	}

	/// <summary>The shared acceleration structure. All cameras trace against this.</summary>
	public static IRayTracingAccelStruct AccelStruct => Instance?._rtAccelStruct;

	/// <summary>
	/// Register a DepthCamera for shared BVH access.
	/// Call from SetupCamera(). Returns false if initialization failed.
	/// </summary>
	public static bool Register(EntityId cameraInstanceId)
	{
		var inst = Instance;
		if (inst == null)
			return false;

		if (inst._rtContext == null)
			inst.Initialize();

		if (inst._rtAccelStruct == null)
			return false;

		if (inst._registeredCameras.Add(cameraInstanceId))
		{
			Debug.Log($"[URTSensorManager] Registered camera {cameraInstanceId}, total={inst._registeredCameras.Count}");
		}

		return true;
	}

	/// <summary>
	/// Unregister a DepthCamera. Call from OnDestroy().
	/// When no cameras remain, shared resources are released.
	/// </summary>
	public static void Unregister(EntityId cameraInstanceId)
	{
		var inst = Instance;
		if (inst == null) return;

		inst._registeredCameras.Remove(cameraInstanceId);

		Debug.Log($"[URTSensorManager] Unregistered camera {cameraInstanceId}, remaining={inst._registeredCameras.Count}");

		if (inst._registeredCameras.Count == 0)
		{
			inst.ReleaseResources();
		}
	}

	/// <summary>
	/// Create an <see cref="IRayTracingShader"/> from the shared context.
	/// The shader is lightweight (no GPU allocations) but must be
	/// disposed by the caller.
	/// </summary>
	public static IRayTracingShader CreateShader(ComputeShader computeShader)
	{
		return Instance?._rtContext?.CreateRayTracingShader(computeShader);
	}

	/// <summary>
	/// Mark the scene as dirty so the BVH is rebuilt on the next frame.
	/// Call this when MeshRenderers are added, removed, enabled, or disabled.
	/// </summary>
	public static void MarkSceneDirty()
	{
		if (s_instance != null)
		{
			s_instance._sceneDirty = true;
		}
	}

	/// <summary>
	/// Ensure the scene BVH is up-to-date for this frame.
	/// Called by each DepthCamera at the start of ExecuteRender.
	/// Only the first caller per frame does actual work; subsequent
	/// calls in the same frame are no-ops.
	/// </summary>
	/// <param name="cmd">CommandBuffer to record the Build into.</param>
	public static void EnsureBVHReady(CommandBuffer cmd)
	{
		using (s_EnsureBVHReadyMarker.Auto())
		{
		var inst = Instance;
		if (inst == null) return;

		if (inst._rtAccelStruct == null)
			return;

		var currentFrame = Time.frameCount;
		if (inst._frameOfLastBuild == currentFrame)
			return; // Already built this frame

		inst._frameOfLastBuild = currentFrame;

		// A URT sensor is rendering this frame and will submit a trace dispatch
		// (against the current TLAS) shortly after — even if we defer the rebuild
		// below. Request a fence in our LateUpdate so the NEXT mutation waits for
		// THIS frame's trace too. Without this, a re-trace submitted on a deferred
		// frame can still be in flight when a later frame disposes/rebuilds the TLAS
		// (the build-only fence does not cover it) -> "missing UAV/input buffer" ->
		// GPU context hang (Xid 109).
		inst._pendingFenceNeeded = true;

		// Frame of the most recent prior trace submission (the previous render frame),
		// captured before we overwrite the render-submit marker for this frame.
		var prevRenderFrame = inst._frameOfLastRenderSubmit;
		inst._frameOfLastRenderSubmit = currentFrame;

		// In-flight TLAS protection: defer all accel-struct mutation (which would
		// immediately dispose the TLAS input buffers in the package) until the GPU
		// has consumed every prior trace dispatch. While deferred, the existing TLAS
		// stays valid and this sensor re-traces against it.
		if (inst._hasBuiltOnce && !inst.IsPriorTraceConsumed(prevRenderFrame))
			return;

		// --- Detect new/removed objects ---
		var realtimeNow = Time.realtimeSinceStartup;

		if (inst._sceneDirty || (realtimeNow - inst._lastSceneGatherTime > inst._sceneGatherInterval))
		{
			// A structural change (object spawned/deleted) makes AddInstance/RemoveInstance
			// reallocate the package's BLAS input buffers and free the old ones IMMEDIATELY.
			// On top of the fence gate above, hard-sync the GPU so no trace dispatch is in
			// flight when those input buffers are freed. This runs only on spawn/delete
			// frames (rare, user-initiated), so the brief stall is acceptable; it does not
			// affect steady-state frames.
			if (inst._sceneDirty)
				AsyncGPUReadback.WaitAllRequests();

			inst.GatherSceneMeshes();
			inst._sceneDirty = false;
		}

		inst.UpdateInstanceTransforms();

		// Skip Build when no geometry is present (prevents SIGSEGV in GraphicsBuffer ctor with count=0)
		if (inst._rtInstances.Count == 0)
			return;

		using (s_BuildBVHMarker.Auto())
		{
			// --- Resize scratch buffer if needed ---
			var scratchHashBefore = inst._rtBuildScratchBuffer?.GetHashCode() ?? 0;
			var instanceCountNow = inst._rtInstances.Count;

			// Grow-with-headroom + deferred dispose: the old buffer (if any) is kept
			// alive for ScratchSafetyFrames so an in-flight Build() referencing it is
			// not corrupted. See GrowScratch.
			inst._rtBuildScratchBuffer = GrowScratch(
				inst._rtBuildScratchBuffer, inst._rtAccelStruct.GetBuildScratchBufferRequiredSizeInBytes());

			var scratchHashAfter = inst._rtBuildScratchBuffer?.GetHashCode() ?? 0;

			// Record into ring buffer only on change — avoids log spam
			if (scratchHashAfter != inst._diagPrevScratchHash || instanceCountNow != inst._diagPrevInstanceCount)
			{
				var reallocated = scratchHashBefore != scratchHashAfter;
				var msg = $"[URT-DIAG] frame={Time.frameCount}"
					+ $" instances:{inst._diagPrevInstanceCount}\u2192{instanceCountNow}"
					+ $" scratchHash:0x{inst._diagPrevScratchHash:X}\u21920x{scratchHashAfter:X}"
					+ $" reallocated:{reallocated}";
				inst.DiagRecord(msg);
				if (reallocated)
					Debug.Log(msg + " (scratch grown; old buffer deferred-freed)");
				else
					Debug.Log(msg);
				inst._diagPrevInstanceCount = instanceCountNow;
				inst._diagPrevScratchHash = scratchHashAfter;
			}

			// --- Build BVH ---
			inst._rtAccelStruct.Build(cmd, inst._rtBuildScratchBuffer);

			// A TLAS now exists; the gate engages from here on. (The per-frame fence
			// that the next mutation waits on is requested at the top of this method,
			// covering both builds and deferred-frame re-traces.)
			inst._hasBuiltOnce = true;
		}
		}
	}

	/// <summary>
	/// True when the GPU has finished every prior trace dispatch that reads the TLAS,
	/// so it is safe to mutate (and thus dispose) the accel struct again. Uses a
	/// GraphicsFence (refreshed every render frame, so it covers deferred-frame
	/// re-traces too) when supported; otherwise falls back to requiring a safe frame
	/// gap since the most recent trace submission.
	/// </summary>
	private bool IsPriorTraceConsumed(int prevRenderFrame)
	{
		if (s_graphicsFenceSupported)
			return !_hasBuildFence || _buildFence.passed;

		return prevRenderFrame < 0 || Time.frameCount - prevRenderFrame >= ScratchSafetyFrames;
	}

	#endregion

	#region "Initialization / Teardown"

	private void Initialize()
	{
		var resources = new RayTracingResources();
		resources.LoadFromURTResourcesByManual();

		var backend = SelectBackend();

		// Context / acceleration-structure creation runs real GPU work. On some
		// Linux/Vulkan drivers the Hardware ray-tracing path faults inside the
		// driver and SIGSEGVs the native render thread. Fail soft: on any error,
		// leave _rtContext/_rtAccelStruct null so Register() returns false and the
		// sensors skip URT rendering (degraded but alive) instead of crashing.
		try
		{
			_rtContext = new RayTracingContext(backend, resources);

			_rtAccelStruct = _rtContext.CreateAccelerationStructure(
				new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastBuild });

			Debug.Log($"[URTSensorManager] Initialized context with backend: {backend}");
		}
		catch (Exception e)
		{
			Debug.LogError($"[URTSensorManager] Failed to initialize ray tracing context (backend={backend}): {e.Message}. URT sensors (lidar/depth camera) will be disabled.");

			_rtAccelStruct?.Dispose();
			_rtAccelStruct = null;
			_rtContext?.Dispose();
			_rtContext = null;
		}
	}

	/// <summary>
	/// Choose the Unified Ray Tracing backend.
	/// Default: Compute on Vulkan/Linux (the Hardware backend faults on many
	/// Linux GPU drivers), auto elsewhere. Override with the CLOISIM_URT_BACKEND
	/// environment variable: "compute" | "hardware" | "auto".
	/// Hardware is only selected when both the preference allows it and the
	/// device actually reports support.
	/// </summary>
	private static RayTracingBackend SelectBackend()
	{
		var pref = (Environment.GetEnvironmentVariable("CLOISIM_URT_BACKEND") ?? string.Empty).Trim().ToLowerInvariant();

		if (string.IsNullOrEmpty(pref))
		{
			var isVulkanOrLinux =
				SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan ||
				Application.platform == RuntimePlatform.LinuxPlayer ||
				Application.platform == RuntimePlatform.LinuxEditor;
			pref = isVulkanOrLinux ? "compute" : "auto";
		}

		var hardwareSupported = RayTracingContext.IsBackendSupported(RayTracingBackend.Hardware);

		switch (pref)
		{
			case "compute":
				return RayTracingBackend.Compute;

			case "hardware":
				if (!hardwareSupported)
					Debug.LogWarning("[URTSensorManager] CLOISIM_URT_BACKEND=hardware requested but the device reports no hardware ray-tracing support; falling back to Compute.");
				return hardwareSupported ? RayTracingBackend.Hardware : RayTracingBackend.Compute;

			case "auto":
			default:
				return hardwareSupported ? RayTracingBackend.Hardware : RayTracingBackend.Compute;
		}
	}

	/// <summary>
	/// Return a scratch GraphicsBuffer at least <paramref name="requiredBytes"/> large,
	/// growing with headroom and never shrinking. When a larger buffer is needed the
	/// old one is queued for deferred disposal (see <see cref="DeferScratchFree"/>)
	/// rather than freed immediately, so a still-in-flight Build()/Dispatch() that
	/// references it is not corrupted. Safe to call every frame.
	/// </summary>
	public static GraphicsBuffer GrowScratch(GraphicsBuffer current, ulong requiredBytes)
	{
		if (requiredBytes == 0)
			return current;

		var capacity = (current != null) ? (ulong)((long)current.count * current.stride) : 0UL;
		if (current != null && capacity >= requiredBytes)
			return current;

		var targetBytes = (ulong)(requiredBytes * ScratchHeadroomFactor);
		var count = (int)((targetBytes + 3) / 4); // stride 4, round up
		var grown = new GraphicsBuffer(RayTracingHelper.ScratchBufferTarget, count, 4);

		if (current != null)
			DeferScratchFree(current);

		return grown;
	}

	/// <summary>
	/// Queue an old scratch buffer for disposal once the GPU has finished the
	/// dispatches that referenced it. A GraphicsFence is captured now: the buffer's
	/// last use was a dispatch submitted before this point, so it is safe to free
	/// the moment the fence passes (frame-count fallback when fences are unsupported).
	/// </summary>
	public static void DeferScratchFree(GraphicsBuffer buffer)
	{
		if (buffer == null)
			return;

		var inst = Instance;
		if (inst == null)
		{
			// No manager to track GPU progress (e.g. during shutdown): dispose now.
			buffer.Dispose();
			return;
		}

		GraphicsFence fence = default;
		var hasFence = false;
		if (s_graphicsFenceSupported)
		{
			fence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.ComputeProcessing);
			hasFence = true;
		}

		inst._deferredScratchFree.Enqueue((buffer, fence, hasFence, Time.frameCount));
	}

	/// <summary>Dispose deferred scratch buffers whose GPU work has completed (fence passed).</summary>
	private void DrainDeferredScratchFrees(bool force = false)
	{
		var currentFrame = Time.frameCount;
		while (_deferredScratchFree.Count > 0)
		{
			var entry = _deferredScratchFree.Peek();
			if (!force)
			{
				var safe = entry.hasFence
					? entry.fence.passed
					: (currentFrame - entry.frame >= ScratchSafetyFrames);
				if (!safe)
					break;
			}

			_deferredScratchFree.Dequeue();
			entry.buffer?.Dispose();
		}
	}

	private void LateUpdate()
	{
		DrainDeferredScratchFrees();

		// Runs after SensorRenderManager.LateUpdate (forced via DefaultExecutionOrder),
		// so every URT trace dispatch for this frame is already submitted. Capture a
		// fence past those dispatches; the next EnsureBVHReady defers mutation until it
		// passes. Only needed on frames where a build actually happened.
		if (_pendingFenceNeeded && s_graphicsFenceSupported)
		{
			_buildFence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.ComputeProcessing);
			_hasBuildFence = true;
		}
		_pendingFenceNeeded = false;
	}

	private void ReleaseResources()
	{
		// GPU is quiescent here (callers drain AsyncGPUReadback first); free every
		// deferred buffer regardless of frame age before tearing the rest down.
		DrainDeferredScratchFrees(force: true);

		_rtBuildScratchBuffer?.Dispose();
		_rtBuildScratchBuffer = null;

		_rtAccelStruct?.Dispose();
		_rtAccelStruct = null;

		_rtContext?.Dispose();
		_rtContext = null;

		_rtInstances.Clear();
		_lastSceneGatherTime = 0f;
		_frameOfLastBuild = -1;

		_diagHistory.Clear();
		_diagPrevInstanceCount = -1;

		_hasBuildFence = false;
		_pendingFenceNeeded = false;
		_hasBuiltOnce = false;
		_frameOfLastRenderSubmit = -1;

		Debug.Log("[URTSensorManager] Released shared URT resources");
	}

	/// <summary>
	/// Drop and recreate the shared acceleration structure from a clean slate at
	/// the simulation-reset boundary. A reset repositions every model at once;
	/// rebuilding the BVH incrementally (RemoveInstance/UpdateInstanceTransform on
	/// the existing handles) over that mass change can leave a compute dispatch
	/// bound to an incompatible/freed buffer ("missing UAV ID ... incompatible
	/// ComputeBuffer") and freeze the GPU. Recreating the accel struct guarantees
	/// no stale handles: the next EnsureBVHReady re-gathers all renderers and
	/// builds fresh.
	///
	/// REQUIRES the GPU to be quiescent for sensor work — callers must pause
	/// rendering (SensorRenderManager.Pause) and drain AsyncGPUReadback before
	/// calling, so no in-flight dispatch still references the old accel struct.
	/// </summary>
	public static void ResetScene()
	{
		if (s_instance != null)
		{
			s_instance.ResetSceneInternal();
		}
	}

	private void ResetSceneInternal()
	{
		if (_rtContext == null || _rtAccelStruct == null)
			return;

		// GPU is quiescent here (caller paused rendering + drained readbacks);
		// free every deferred scratch buffer before recreating the accel struct.
		DrainDeferredScratchFrees(force: true);

		try
		{
			_rtAccelStruct.Dispose();
			_rtAccelStruct = _rtContext.CreateAccelerationStructure(
				new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastBuild });
		}
		catch (Exception e)
		{
			Debug.LogError($"[URTSensorManager] Failed to recreate acceleration structure on reset: {e.Message}. URT sensors disabled.");
			_rtAccelStruct?.Dispose();
			_rtAccelStruct = null;
			return;
		}

		_rtInstances.Clear();
		_existingInstanceKeys.Clear();
		_desiredInstanceKeys.Clear();

		// Force a full re-gather + rebuild on the next EnsureBVHReady after resume.
		_sceneDirty = true;
		_frameOfLastBuild = -1;
		_lastSceneGatherTime = 0f;
		_diagPrevInstanceCount = -1;

		// Old TLAS is gone; do not gate the first post-reset rebuild on a stale fence.
		_hasBuildFence = false;
		_pendingFenceNeeded = false;
		_hasBuiltOnce = false;
		_frameOfLastRenderSubmit = -1;

		Debug.Log("[URTSensorManager] Acceleration structure reset (clean rebuild scheduled)");
	}

	#endregion

	#region "Scene Gathering"

	/// <summary>
	/// Gather all active MeshRenderers in the scene that match the
	/// Default+Plane culling mask and populate the shared accel structure.
	/// </summary>
	private void GatherSceneMeshes()
	{
		using (s_GatherSceneMeshesMarker.Auto())
		{
		if (_rtAccelStruct == null)
			return;

		_existingInstanceKeys.Clear();
		for (var i = 0; i < _rtInstances.Count; i++)
		{
			var entry = _rtInstances[i];
			_existingInstanceKeys.Add(new InstanceKey(entry.renderer, entry.mesh, entry.subMeshIndex));
		}

		_desiredInstanceKeys.Clear();

		// Use FindObjectsByType for active scene renderers only (cheaper than Resources.FindObjectsOfTypeAll)
		var renderers = FindObjectsByType<MeshRenderer>();

		int addedCount = 0;
		int removedCount = 0;
		int skippedLayer = 0;
		int skippedNoMesh = 0;
		int skippedError = 0;

		foreach (var renderer in renderers)
		{
			if (!renderer.gameObject.scene.isLoaded)
				continue;

			if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
				continue;

			if ((_cullingMask & (1 << renderer.gameObject.layer)) == 0)
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
				var key = new InstanceKey(renderer, mesh, sub);
				_desiredInstanceKeys.Add(key);

				if (_existingInstanceKeys.Contains(key))
					continue;

				try
				{
					var desc = new MeshInstanceDesc(mesh, sub)
					{
						localToWorldMatrix = renderer.localToWorldMatrix,
						mask = 0xFF,
						enableTriangleCulling = false,
						opaqueGeometry = true
					};

					var handle = _rtAccelStruct.AddInstance(desc);
					_rtInstances.Add(new InstanceEntry
					{
						renderer = renderer,
						mesh = mesh,
						subMeshIndex = sub,
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

		for (var i = _rtInstances.Count - 1; i >= 0; i--)
		{
			var entry = _rtInstances[i];
			var key = new InstanceKey(entry.renderer, entry.mesh, entry.subMeshIndex);
			if (_desiredInstanceKeys.Contains(key))
				continue;

			try
			{
				_rtAccelStruct.RemoveInstance(entry.handle);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Failed to remove '{entry.renderer?.name ?? "<destroyed>"}' " +
					$"sub={entry.subMeshIndex} mesh='{entry.mesh?.name ?? "<unknown>"}': {e.Message}");
			}

			_rtInstances.RemoveAt(i);
			removedCount++;
		}

		_lastSceneGatherTime = Time.realtimeSinceStartup;

#if UNITY_EDITOR
		Debug.Log($"[URTSensorManager] GatherSceneMeshes: added={addedCount}, removed={removedCount}, " +
			$"skippedLayer={skippedLayer}, skippedNoMesh={skippedNoMesh}, " +
			$"skippedError={skippedError}, totalRenderers={renderers.Length}");
#endif
		// The more renderers, the larger the interval, making the periodic refresh less frequent for complex scenes
		_sceneGatherInterval = MinSceneGatherInterval + (float)renderers.Length / RendererCountScalingFactor;

		// Allocate / resize the build scratch buffer (grow-with-headroom + deferred dispose)
		_rtBuildScratchBuffer = GrowScratch(
			_rtBuildScratchBuffer, _rtAccelStruct.GetBuildScratchBufferRequiredSizeInBytes());
		}
	}

	/// <summary>
	/// Update world-space transforms for all cached instances.
	/// </summary>
	private void UpdateInstanceTransforms()
	{
		using (s_UpdateInstanceTransformsMarker.Auto())
		{
		bool needsRebuild = false;

		for (int i = _rtInstances.Count - 1; i >= 0; i--)
		{
			var entry = _rtInstances[i];
			if (entry.renderer == null || !entry.renderer.gameObject.activeInHierarchy)
			{
				// Remove stale instance immediately to prevent invalid handles reaching Build
				try
				{
					_rtAccelStruct.RemoveInstance(entry.handle);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"[URTSensorManager] Failed to remove stale instance " +
						$"'{entry.renderer?.name ?? "<destroyed>"}': {e.Message}");
				}
				_rtInstances.RemoveAt(i);
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
