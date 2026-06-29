/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Reflection;
using System.Threading;
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
/// (context, acceleration structures, build scratch buffer, scene gathering).
/// Multiple sensors share a single BVH set instead of each building their own.
///
/// Double-buffered acceleration structures: _rtAccelStructs[_readIdx] is the
/// current trace target (sensors ray-trace against it); _rtAccelStructs[_writeIdx]
/// is the current build target (updated with fresh transforms and rebuilt).
/// They swap every frame after a successful build.  This eliminates the
/// "stale-TLAS re-trace" that single-buffer mode incurred when the GPU fence
/// had not passed (e.g. DVFS cold-start) — in that case the write struct
/// is skipped but the read struct is always last-frame-fresh, bounding
/// staleness to exactly 1 build cycle instead of N cycles.
///
/// Per-sensor resources (trace scratch buffer, command buffer, output
/// compute buffers) remain owned by each sensor instance.
/// </summary>
// Execution order is forced LATE so this component's LateUpdate runs *after*
// SensorRenderManager.LateUpdate (default order 0), which is where every URT
// sensor records and submits its trace dispatch for the frame.  The per-struct
// trace fences (see IsPriorTraceConsumed) are created in our LateUpdate and
// must capture those already-submitted dispatches, so we must run after them.
[DefaultExecutionOrder(1000)]
public class URTSensorManager : MonoBehaviour
{
	private static URTSensorManager s_instance;
	private static bool s_applicationQuitting = false;

	/// <summary>
	/// Incremented every time the acceleration structures are recreated (i.e. on
	/// simulation reset). Per-sensor consumers compare their cached generation to
	/// this value; a mismatch means per-sensor URT resources must be rebuilt so
	/// they do not hold stale binding state from the disposed accel structs.
	/// </summary>
	private int _rtAccelStructGeneration = 0;
	public static int AccelStructGeneration => s_instance?._rtAccelStructGeneration ?? 0;

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
	/// Append a per-sensor diagnostic entry (e.g. buffer recreation with GPU handle
	/// hashes) into the shared URT ring buffer so it appears in DumpDiagHistory when
	/// a "missing UAV"/"incompatible ComputeBuffer" GPU fault is later detected.
	/// </summary>
	public static void DiagLog(string entry)
	{
		s_instance?.DiagRecord(entry);
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
		sb.AppendLine($"[URT-DIAG] generation={inst._rtAccelStructGeneration}"
			+ $" readIdx={inst._readIdx} writeIdx={inst._writeIdx}"
			+ $" hasTlas=[{inst._structHasTlas[0]},{inst._structHasTlas[1]}]"
			+ $" accelNull=[{inst._rtAccelStructs[0] == null},{inst._rtAccelStructs[1] == null}]"
			+ $" frameOfLastBuild={inst._frameOfLastBuild}");
		var bs = inst._rtBuildScratchBuffer;
		sb.AppendLine($"[URT-DIAG] buildScratch: "
			+ (bs == null ? "null" : $"hash=0x{bs.GetHashCode():X} valid={bs.IsValid()} count={bs.count} stride={bs.stride}")
			+ $" | deferredScratchFree={inst._deferredScratchFree.Count} deferredDispose={inst._deferredDispose.Count}");
		sb.AppendLine("[URT-DIAG] --- recent state changes (oldest first) ---");
		foreach (var line in inst._diagHistory)
			sb.AppendLine(line);
		sb.AppendLine("[URT-DIAG] === End of history ===");
		Debug.LogError(sb.ToString());
	}

	#endregion

	#region "Shared URT resources"

	private RayTracingContext _rtContext;
	private GraphicsBuffer _rtBuildScratchBuffer;

	#region "Double-buffered acceleration structures"
	// Two acceleration structures ping-pong each frame:
	//   _rtAccelStructs[_readIdx]  — sensors trace against this (last-built)
	//   _rtAccelStructs[_writeIdx] — we gather+transform-update+build into this
	//
	// After a successful build we swap _readIdx/_writeIdx so the freshly-built
	// struct becomes the new read target immediately.
	//
	// When the per-struct fence hasn't passed (writeIdx was traced recently and
	// the GPU hasn't finished yet — typical during DVFS cold-start), we skip the
	// build for that cycle.  The sensors still trace against _readIdx, which was
	// built at most 1 frame ago, bounding staleness to 1 cycle.
	//
	// Contrast with the old single-buffer approach: a fence miss there caused an
	// unknown number of stale cycles (up to N during GPU ramp-up), accumulating
	// positional error that appeared as lidar jitter until the GPU warmed up.

	private readonly IRayTracingAccelStruct[] _rtAccelStructs = new IRayTracingAccelStruct[2];
	private int _readIdx = 0;  // current trace target (sensors read this)
	private int _writeIdx = 1; // current build target (we write into this)

	// Per-struct instance state — each struct tracks its own instance handles.
	// When a struct becomes the write target it re-gathers independently, so
	// the two structs stay consistent without cross-struct handle copies.
	private readonly List<InstanceEntry>[] _perStructInstances =
		new[] { new List<InstanceEntry>(), new List<InstanceEntry>() };
	private readonly HashSet<InstanceKey>[] _perStructDesiredKeys =
		new[] { new HashSet<InstanceKey>(), new HashSet<InstanceKey>() };
	private readonly HashSet<InstanceKey>[] _perStructExistingKeys =
		new[] { new HashSet<InstanceKey>(), new HashSet<InstanceKey>() };
	private readonly float[] _perStructLastGatherTime = new float[2];
	private readonly bool[] _perStructDirty = new[] { true, true };

	// Whether each struct has ever been successfully built (i.e. Build() was called with
	// non-empty instances).  Prevents AccelStruct from returning a struct whose
	// m_TopLevelAccelStruct is still null, which would cause downstream Bind() calls to
	// pass null UAV buffers and trigger a "missing UAV" GPU error.
	private readonly bool[] _structHasTlas = new bool[2];

	// Per-struct trace fences: covers all trace dispatches that READ _rtAccelStructs[i].
	// Before building into _rtAccelStructs[writeIdx] we check _traceFences[writeIdx]
	// to ensure the GPU has finished all previous traces that read it.
	private static readonly bool s_graphicsFenceSupported = SystemInfo.supportsGraphicsFence;
	private readonly GraphicsFence[] _traceFences = new GraphicsFence[2];
	private readonly bool[] _hasTraceFence = new bool[2];
	// No-fence fallback: frame number of the most recent trace that read each struct.
	private readonly int[] _frameOfLastTrace = new[] { -1, -1 };
	#endregion

	#region "Safe scratch-buffer management"
	// See original comments — logic unchanged; only the per-struct accel structs
	// replace the old single _rtAccelStruct.

	/// <summary>Extra capacity multiplier applied when (re)allocating a scratch buffer.</summary>
	private const float ScratchHeadroomFactor = 2.0f;

	/// <summary>
	/// Fallback only (GraphicsFence unsupported): frames an old scratch buffer is kept
	/// alive before disposal.
	/// </summary>
	private const int ScratchSafetyFrames = 6;

	private readonly Queue<(GraphicsBuffer buffer, GraphicsFence fence, bool hasFence, int frame)> _deferredScratchFree = new();

	// Deferred disposal of PER-SENSOR GPU buffers (ComputeBuffer / GraphicsBuffer)
	// recreated at the accel-struct-reset boundary (Lidar/DepthCamera
	// RebuildURTPerSensorResources). Disposing them immediately can free a buffer
	// still referenced by an in-flight dispatch (use-after-free → "incompatible
	// ComputeBuffer" / Xid 109 CTX SWITCH TIMEOUT). Gated on a GraphicsFence,
	// identical to the scratch-buffer free path above.
	private readonly Queue<(IDisposable resource, GraphicsFence fence, bool hasFence, int frame)> _deferredDispose = new();
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

	private const float MinSceneGatherInterval = 10.0f;
	private const float RendererCountScalingFactor = 500.0f;

	[SerializeField]
	private float _sceneGatherInterval = MinSceneGatherInterval;

	private int _cullingMask;

	/// <summary>
	/// Cameras registered for shared BVH access.
	/// </summary>
	private readonly HashSet<EntityId> _registeredCameras = new();

	[SerializeField]
	private int _frameOfLastBuild = -1;

	// Whether a fence should be captured this LateUpdate for the traced struct.
	// _pendingFenceIdx records which struct index sensors traced this frame so that
	// LateUpdate stamps the fence on the correct struct even after a readIdx/writeIdx swap.
	private bool _pendingFenceNeeded;
	private int _pendingFenceIdx;

	// Post-reset cooldown: EnsureBVHReady skips building until this frame is reached.
	// Set to currentFrame + 3 on a clean reset, + 60 if WaitForGPUQuiescence timed out.
	// Prevents dispatching against a freshly-reset TLAS while the GPU may still be
	// recovering from a prior dispatch error (fence timeout → corrupted Build() output
	// → "missing UAV" on first post-rebuild trace).
	private int _postResetResumeBuildFrame = 0;

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

	/// <summary>
	/// The current read-side acceleration structure. All sensors trace against this.
	/// Updated (via swap) immediately after each successful build in EnsureBVHReady.
	/// Returns null when the struct has never been built (TLAS is null), preventing
	/// downstream Bind() calls from passing null UAV buffers to the GPU.
	/// </summary>
	public static IRayTracingAccelStruct AccelStruct
	{
		get
		{
			var inst = Instance;
			if (inst == null || !inst._structHasTlas[inst._readIdx])
				return null;
			return inst._rtAccelStructs[inst._readIdx];
		}
	}

	/// <summary>
	/// Register a sensor for shared BVH access.
	/// Call from SetupCamera(). Returns false if initialization failed.
	/// </summary>
	public static bool Register(EntityId cameraInstanceId)
	{
		var inst = Instance;
		if (inst == null)
			return false;

		if (inst._rtContext == null)
			inst.Initialize();

		if (inst._rtAccelStructs[0] == null)
			return false;

		if (inst._registeredCameras.Add(cameraInstanceId))
		{
			Debug.Log($"[URTSensorManager] Registered camera {cameraInstanceId}, total={inst._registeredCameras.Count}");
		}

		return true;
	}

	/// <summary>
	/// Unregister a sensor. Call from OnDestroy().
	/// When no sensors remain, shared resources are released.
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
	/// Mark the scene as dirty so both BVH structs are re-gathered on
	/// their next respective build cycles.
	/// </summary>
	public static void MarkSceneDirty()
	{
		if (s_instance != null)
		{
			s_instance._perStructDirty[0] = true;
			s_instance._perStructDirty[1] = true;
		}
	}

	/// <summary>
	/// Ensure the scene BVH is up-to-date for this frame.
	/// Called by each URT sensor at the start of its render method.
	/// Only the first caller per frame does actual work; subsequent
	/// calls in the same frame are no-ops (they use the same freshly-built
	/// read struct via AccelStruct).
	/// </summary>
	/// <param name="cmd">CommandBuffer to record the Build into.</param>
	public static void EnsureBVHReady(CommandBuffer cmd)
	{
		using (s_EnsureBVHReadyMarker.Auto())
		{
		var inst = Instance;
		if (inst == null) return;

		if (inst._rtAccelStructs[inst._readIdx] == null)
			return;

		var currentFrame = Time.frameCount;
		if (inst._frameOfLastBuild == currentFrame)
			return; // Already built this frame

		// Post-reset cooldown: GPU may still be recovering from a dispatch error
		// (WaitForGPUQuiescence timed out). Skipping the build keeps AccelStruct null
		// so sensors skip dispatch — prevents "missing UAV" on the first post-rebuild trace.
		if (currentFrame < inst._postResetResumeBuildFrame)
			return;

		inst._frameOfLastBuild = currentFrame;

		// This frame's sensors will trace against _readIdx — request a fence
		// in LateUpdate so the NEXT write-cycle on that struct waits for them.
		// Save the current readIdx here: EnsureBVHReady may swap readIdx/writeIdx
		// before LateUpdate runs, so we must record the traced index now.
		inst._pendingFenceNeeded = true;
		inst._pendingFenceIdx = inst._readIdx;

		// Record that _readIdx is being traced this frame (no-fence fallback).
		inst._frameOfLastTrace[inst._readIdx] = currentFrame;

		// In-flight protection for writeIdx: if the GPU is still processing
		// prior traces that read _rtAccelStructs[writeIdx] (it was _readIdx
		// last frame), skip the build for this cycle.  Sensors re-trace
		// _readIdx as-is — at most 1 frame stale, never N-frame-accumulating.
		if (!inst.IsPriorTraceConsumed(inst._writeIdx))
			return;

		var writeIdx = inst._writeIdx;
		var writeStruct = inst._rtAccelStructs[writeIdx];
		if (writeStruct == null)
			return;

		// --- Detect new/removed objects ---
		var realtimeNow = Time.realtimeSinceStartup;

		if (inst._perStructDirty[writeIdx] ||
			(realtimeNow - inst._perStructLastGatherTime[writeIdx] > inst._sceneGatherInterval))
		{
			inst.GatherSceneMeshes(writeIdx);
			inst._perStructDirty[writeIdx] = false;
		}

		inst.UpdateInstanceTransforms(writeIdx);

		// Skip Build when no geometry is present.
		// Do NOT swap — promoting a never-built struct to readIdx would make AccelStruct
		// return a struct whose m_TopLevelAccelStruct is null.  Sensors calling Bind()
		// on that struct would pass null UAV buffers and trigger a GPU dispatch error.
		// Re-set dirty so GatherSceneMeshes re-runs next frame instead of waiting
		// for the 10-second interval (covers the early-loading window).
		if (inst._perStructInstances[writeIdx].Count == 0)
		{
			inst._perStructDirty[writeIdx] = true;
			return;
		}

		CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:BuildAS");
		using (s_BuildBVHMarker.Auto())
		{
			// --- Resize scratch buffer if needed ---
			var scratchHashBefore = inst._rtBuildScratchBuffer?.GetHashCode() ?? 0;
			var instanceCountNow = inst._perStructInstances[writeIdx].Count;

			inst._rtBuildScratchBuffer = GrowScratch(
				inst._rtBuildScratchBuffer, writeStruct.GetBuildScratchBufferRequiredSizeInBytes());

			var scratchHashAfter = inst._rtBuildScratchBuffer?.GetHashCode() ?? 0;

			// Record into ring buffer only on change
			if (scratchHashAfter != inst._diagPrevScratchHash || instanceCountNow != inst._diagPrevInstanceCount)
			{
				var reallocated = scratchHashBefore != scratchHashAfter;
				var msg = $"[URT-DIAG] frame={Time.frameCount}"
					+ $" instances:{inst._diagPrevInstanceCount}→{instanceCountNow}"
					+ $" scratchHash:0x{inst._diagPrevScratchHash:X}→0x{scratchHashAfter:X}"
					+ $" reallocated:{reallocated}";
				inst.DiagRecord(msg);
				if (reallocated)
					Debug.Log(msg + " (scratch grown; old buffer deferred-freed)");
				else
					Debug.Log(msg);
				inst._diagPrevInstanceCount = instanceCountNow;
				inst._diagPrevScratchHash = scratchHashAfter;
			}

			// --- Build BVH into write struct ---
			writeStruct.Build(cmd, inst._rtBuildScratchBuffer);
		}

		// Swap: freshly-built writeIdx becomes new readIdx.
		// AccelStruct now returns the struct we just built, so sensors that
		// call SetAccelerationStructure(AccelStruct) after EnsureBVHReady
		// automatically trace against up-to-date transforms.
		(inst._readIdx, inst._writeIdx) = (inst._writeIdx, inst._readIdx);

		// Mark the new readIdx as having a valid TLAS so AccelStruct returns it.
		inst._structHasTlas[inst._readIdx] = true;
		}
	}

	/// <summary>
	/// True when the GPU has finished every prior trace dispatch that reads
	/// _rtAccelStructs[structIdx], so it is safe to mutate (FreeTopLevelAccelStruct
	/// will be called internally by AddInstance / UpdateInstanceTransform / etc.).
	/// Uses a per-struct GraphicsFence (refreshed every frame the struct is traced)
	/// when supported; falls back to a frame-count gap otherwise.
	/// </summary>
	private bool IsPriorTraceConsumed(int structIdx)
	{
		if (s_graphicsFenceSupported)
			return !_hasTraceFence[structIdx] || _traceFences[structIdx].passed;

		var prevFrame = _frameOfLastTrace[structIdx];
		return prevFrame < 0 || Time.frameCount - prevFrame >= ScratchSafetyFrames;
	}

	#endregion

	#region "Initialization / Teardown"

	private void Initialize()
	{
		var resources = new RayTracingResources();
		resources.LoadFromURTResourcesByManual();

		var backend = SelectBackend();

		try
		{
			_rtContext = new RayTracingContext(backend, resources);

			for (var i = 0; i < 2; i++)
			{
				_rtAccelStructs[i] = _rtContext.CreateAccelerationStructure(
					new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastBuild });
			}

			Debug.Log($"[URTSensorManager] Initialized context with backend: {backend}");
		}
		catch (Exception e)
		{
			Debug.LogError($"[URTSensorManager] Failed to initialize ray tracing context (backend={backend}): {e.Message}. URT sensors (lidar/depth camera) will be disabled.");

			for (var i = 0; i < 2; i++)
			{
				_rtAccelStructs[i]?.Dispose();
				_rtAccelStructs[i] = null;
			}
			_rtContext?.Dispose();
			_rtContext = null;
		}
	}

	/// <summary>
	/// Choose the Unified Ray Tracing backend.
	/// Default: Compute on Vulkan/Linux; auto elsewhere.
	/// Override with CLOISIM_URT_BACKEND env var: "compute" | "hardware" | "auto".
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
	/// growing with headroom and never shrinking. Old buffers are deferred-freed.
	/// </summary>
	public static GraphicsBuffer GrowScratch(GraphicsBuffer current, ulong requiredBytes)
	{
		if (requiredBytes == 0)
			return current;

		var capacity = (current != null) ? (ulong)((long)current.count * current.stride) : 0UL;
		if (current != null && capacity >= requiredBytes)
			return current;

		var targetBytes = (ulong)(requiredBytes * ScratchHeadroomFactor);
		var count = (int)((targetBytes + 3) / 4);
		var grown = new GraphicsBuffer(RayTracingHelper.ScratchBufferTarget, count, 4);

		if (current != null)
			DeferScratchFree(current);

		return grown;
	}

	/// <summary>
	/// Queue an old scratch buffer for disposal once the GPU has finished the
	/// dispatches that referenced it.
	/// </summary>
	public static void DeferScratchFree(GraphicsBuffer buffer)
	{
		if (buffer == null)
			return;

		var inst = Instance;
		if (inst == null)
		{
			buffer.Dispose();
			return;
		}

		GraphicsFence fence = default;
		var hasFence = false;
		if (s_graphicsFenceSupported)
		{
			fence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
			hasFence = true;
		}

		inst._deferredScratchFree.Enqueue((buffer, fence, hasFence, Time.frameCount));
	}

	/// <summary>Dispose deferred scratch buffers whose GPU work has completed.</summary>
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

	/// <summary>
	/// Queue a per-sensor GPU buffer (ComputeBuffer / GraphicsBuffer) for disposal
	/// once the GPU has finished any dispatch that may still reference it. Use this
	/// instead of an immediate Dispose()/Release() when recreating per-sensor buffers
	/// at the accel-struct-reset boundary, where prior dispatches may still be in
	/// flight (immediate free → "incompatible ComputeBuffer" / Xid 109).
	/// </summary>
	public static void DeferDispose(IDisposable resource)
	{
		if (resource == null)
			return;

		var inst = Instance;
		if (inst == null)
		{
			resource.Dispose();
			return;
		}

		GraphicsFence fence = default;
		var hasFence = false;
		if (s_graphicsFenceSupported)
		{
			fence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
			hasFence = true;
		}

		inst._deferredDispose.Enqueue((resource, fence, hasFence, Time.frameCount));
	}

	/// <summary>Dispose deferred per-sensor buffers whose GPU work has completed.</summary>
	private void DrainDeferredDisposes(bool force = false)
	{
		var currentFrame = Time.frameCount;
		while (_deferredDispose.Count > 0)
		{
			var entry = _deferredDispose.Peek();
			if (!force)
			{
				var safe = entry.hasFence
					? entry.fence.passed
					: (currentFrame - entry.frame >= ScratchSafetyFrames);
				if (!safe)
					break;
			}

			_deferredDispose.Dequeue();
			try
			{
				entry.resource?.Dispose();
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Deferred dispose failed: {e.Message}");
			}
		}
	}

	private void LateUpdate()
	{
		// Log when cooldown expires so the log shows exactly when sensors resume tracing.
		if (_postResetResumeBuildFrame > 0 && Time.frameCount == _postResetResumeBuildFrame)
			Debug.Log("[URTSensorManager] Post-reset cooldown expired — BVH rebuild and sensor tracing resuming.");

		DrainDeferredScratchFrees();
		DrainDeferredDisposes();

		// Runs after SensorRenderManager.LateUpdate, so every URT trace dispatch
		// for this frame is already submitted.  Capture a fence for _pendingFenceIdx
		// (the struct sensors actually traced this frame, saved before any swap).
		// Using _readIdx here would be wrong: EnsureBVHReady swaps readIdx/writeIdx
		// after building, so by LateUpdate _readIdx is the newly-built struct (not
		// the one sensors traced), and _writeIdx is what actually needs the fence.
		if (_pendingFenceNeeded && s_graphicsFenceSupported)
		{
			_traceFences[_pendingFenceIdx] = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
			_hasTraceFence[_pendingFenceIdx] = true;
		}
		_pendingFenceNeeded = false;
	}

	private void ReleaseResources()
	{
		DrainDeferredScratchFrees(force: true);
		DrainDeferredDisposes(force: true);

		_rtBuildScratchBuffer?.Dispose();
		_rtBuildScratchBuffer = null;

		for (var i = 0; i < 2; i++)
		{
			_rtAccelStructs[i]?.Dispose();
			_rtAccelStructs[i] = null;
			_perStructInstances[i].Clear();
			_perStructExistingKeys[i].Clear();
			_perStructDesiredKeys[i].Clear();
			_perStructLastGatherTime[i] = 0f;
			_perStructDirty[i] = true;
			_hasTraceFence[i] = false;
			_frameOfLastTrace[i] = -1;
			_structHasTlas[i] = false;
		}

		_rtContext?.Dispose();
		_rtContext = null;

		_readIdx = 0;
		_writeIdx = 1;
		_frameOfLastBuild = -1;
		_pendingFenceNeeded = false;
		_pendingFenceIdx = 0;
		_postResetResumeBuildFrame = 0;

		_diagHistory.Clear();
		_diagPrevInstanceCount = -1;

		Debug.Log("[URTSensorManager] Released shared URT resources");
	}

	/// <summary>
	/// Drop and recreate both shared acceleration structures from a clean slate
	/// at the simulation-reset boundary.
	/// </summary>
	public static void ResetScene()
	{
		if (s_instance != null)
		{
			s_instance.ResetSceneInternal();
		}
	}

	/// <summary>
	/// Spin-wait for all in-flight GPU dispatches to complete.
	/// Called before reset to prevent disposing resources still referenced by
	/// in-flight work. Reset (SignalReset) can fire immediately after a
	/// "missing UAV" GPU dispatch error while the GPU is still executing that
	/// dispatch; disposing resources at that moment causes SIGSEGV in the driver.
	/// Returns true if the GPU quiesced cleanly, false if the 1-second timeout fired
	/// (GPU is in an error/recovery state).
	/// </summary>
	private static bool WaitForGPUQuiescence()
	{
		// Suppress FreezeWatchdog during the intentional spin-wait: this blocks the main
		// thread for up to 1 second and would otherwise fire a false-positive stall warning.
		CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
		try
		{
			AsyncGPUReadback.WaitAllRequests();

			if (!s_graphicsFenceSupported)
				return true;

			// Insert a fence after all currently-submitted GPU work. Once it passes,
			// no prior dispatch can still be accessing GPU memory.
			var fence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);

			var deadline = System.Diagnostics.Stopwatch.GetTimestamp()
				+ System.Diagnostics.Stopwatch.Frequency; // 1-second timeout
			while (!fence.passed)
			{
				if (System.Diagnostics.Stopwatch.GetTimestamp() > deadline)
				{
					Debug.LogWarning("[URTSensorManager] WaitForGPUQuiescence: timed out after 1s — GPU may be in error state, applying extended rebuild cooldown.");
					return false;
				}
				System.Threading.Thread.Sleep(1);
			}
			return true;
		}
		finally
		{
			CLOiSim.Diagnostics.FreezeWatchdog.Restore();
		}
	}

	private void ResetSceneInternal()
	{
		if (_rtContext == null) return;
		if (_rtAccelStructs[0] == null && _rtAccelStructs[1] == null) return;

		// Wait for in-flight GPU dispatches to complete before freeing resources.
		var gpuClean = WaitForGPUQuiescence();

		DrainDeferredScratchFrees(force: true);
		DrainDeferredDisposes(force: true);

		// Drop the build scratch buffer: a freshly-recreated accel struct must not
		// reuse a scratch buffer that was sized/bound for the now-disposed struct
		// (diag showed scratchHash unchanged — reallocated:False — across reset).
		// GPU is quiescent here, so immediate dispose is safe. The next build
		// allocates a new one via GrowScratch.
		_rtBuildScratchBuffer?.Dispose();
		_rtBuildScratchBuffer = null;

		for (var i = 0; i < 2; i++)
		{
			if (_rtAccelStructs[i] == null) continue;
			try
			{
				_rtAccelStructs[i].Dispose();
				_rtAccelStructs[i] = _rtContext.CreateAccelerationStructure(
					new AccelerationStructureOptions { buildFlags = BuildFlags.PreferFastBuild });
			}
			catch (Exception e)
			{
				Debug.LogError($"[URTSensorManager] Failed to recreate acceleration structure[{i}] on reset: {e.Message}. URT sensors disabled.");
				_rtAccelStructs[i]?.Dispose();
				_rtAccelStructs[i] = null;
			}

			_perStructInstances[i].Clear();
			_perStructExistingKeys[i].Clear();
			_perStructDesiredKeys[i].Clear();
			_perStructLastGatherTime[i] = 0f;
			_perStructDirty[i] = true;
			_hasTraceFence[i] = false;
			_frameOfLastTrace[i] = -1;
			_structHasTlas[i] = false;
		}

		_readIdx = 0;
		_writeIdx = 1;
		_frameOfLastBuild = -1;
		_pendingFenceNeeded = false;
		_pendingFenceIdx = 0;
		_diagPrevInstanceCount = -1;

		// Signal per-sensor consumers (DepthCamera, Lidar) that they must recreate
		// their per-sensor shader wrapper so no stale binding to the disposed
		// accel structs remains.
		_rtAccelStructGeneration++;

		// Post-reset cooldown: when the GPU quiescence wait timed out the GPU may still be
		// in an error-recovery state. Building the TLAS while the GPU is unhealthy produces
		// a corrupt acceleration structure, and the very first trace against it triggers
		// another "missing UAV" error + freeze. Hold off on building for 60 frames (~1 s)
		// to allow the GPU driver to complete its TDR/recovery cycle.
		// On a clean reset (fence passed) a 3-frame gap is enough to let any last-frame
		// in-flight work retire before we submit new BVH commands.
		_postResetResumeBuildFrame = Time.frameCount + (gpuClean ? 3 : 60);

		Debug.Log($"[URTSensorManager] Acceleration structures reset (clean rebuild scheduled, cooldown={_postResetResumeBuildFrame - Time.frameCount} frames, gpuClean={gpuClean})");
	}

	#endregion

	#region "Scene Gathering"

	/// <summary>
	/// Gather all active MeshRenderers that match the culling mask and populate
	/// _rtAccelStructs[idx].  Each struct maintains its own independent instance
	/// list; when a struct next becomes the write target its list is refreshed
	/// from scratch via this method.
	/// </summary>
	private void GatherSceneMeshes(int idx)
	{
		using (s_GatherSceneMeshesMarker.Auto())
		{
		var accelStruct = _rtAccelStructs[idx];
		if (accelStruct == null)
			return;

		var instances = _perStructInstances[idx];
		var existingKeys = _perStructExistingKeys[idx];
		var desiredKeys = _perStructDesiredKeys[idx];

		existingKeys.Clear();
		for (var i = 0; i < instances.Count; i++)
		{
			var entry = instances[i];
			existingKeys.Add(new InstanceKey(entry.renderer, entry.mesh, entry.subMeshIndex));
		}

		desiredKeys.Clear();

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
				desiredKeys.Add(key);

				if (existingKeys.Contains(key))
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

					var handle = accelStruct.AddInstance(desc);
					instances.Add(new InstanceEntry
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

		for (var i = instances.Count - 1; i >= 0; i--)
		{
			var entry = instances[i];
			var key = new InstanceKey(entry.renderer, entry.mesh, entry.subMeshIndex);
			if (desiredKeys.Contains(key))
				continue;

			try
			{
				accelStruct.RemoveInstance(entry.handle);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Failed to remove '{entry.renderer?.name ?? "<destroyed>"}' " +
					$"sub={entry.subMeshIndex} mesh='{entry.mesh?.name ?? "<unknown>"}': {e.Message}");
			}

			instances.RemoveAt(i);
			removedCount++;
		}

		_perStructLastGatherTime[idx] = Time.realtimeSinceStartup;

#if UNITY_EDITOR
		Debug.Log($"[URTSensorManager] GatherSceneMeshes[{idx}]: added={addedCount}, removed={removedCount}, " +
			$"skippedLayer={skippedLayer}, skippedNoMesh={skippedNoMesh}, " +
			$"skippedError={skippedError}, totalRenderers={renderers.Length}");
#endif
		_sceneGatherInterval = MinSceneGatherInterval + (float)renderers.Length / RendererCountScalingFactor;

		// Allocate / resize the build scratch buffer
		_rtBuildScratchBuffer = GrowScratch(
			_rtBuildScratchBuffer, accelStruct.GetBuildScratchBufferRequiredSizeInBytes());
		}
	}

	/// <summary>
	/// Update world-space transforms for all cached instances in _rtAccelStructs[idx].
	/// </summary>
	private void UpdateInstanceTransforms(int idx)
	{
		using (s_UpdateInstanceTransformsMarker.Auto())
		{
		var accelStruct = _rtAccelStructs[idx];
		if (accelStruct == null)
			return;

		var instances = _perStructInstances[idx];
		bool needsRebuild = false;

		for (int i = instances.Count - 1; i >= 0; i--)
		{
			var entry = instances[i];
			if (entry.renderer == null || !entry.renderer.gameObject.activeInHierarchy)
			{
				try
				{
					accelStruct.RemoveInstance(entry.handle);
				}
				catch (Exception e)
				{
					Debug.LogWarning($"[URTSensorManager] Failed to remove stale instance " +
						$"'{entry.renderer?.name ?? "<destroyed>"}': {e.Message}");
				}
				instances.RemoveAt(i);
				needsRebuild = true;
				continue;
			}

			accelStruct.UpdateInstanceTransform(entry.handle, entry.renderer.localToWorldMatrix);
		}

		if (needsRebuild)
		{
			_perStructLastGatherTime[idx] = 0f; // Force re-gather next call
		}
		}
	}

	#endregion

	#region "Unity lifecycle"

	// Throttle: timestamp of last fault dump (Stopwatch ticks). Thread-safe via
	// Interlocked — logMessageReceivedThreaded fires from the render thread, not main.
	private long _lastFaultDumpTicks = long.MinValue;

	private void OnEnable()
	{
		// logMessageReceivedThreaded catches messages from all threads, including the
		// render thread where GPU compute dispatch faults ("missing UAV") are logged.
		Application.logMessageReceivedThreaded += OnUnityLogMessage;
	}

	private void OnDisable()
	{
		Application.logMessageReceivedThreaded -= OnUnityLogMessage;
	}

	/// <summary>
	/// Detect Unity's native GPU dispatch faults ("missing UAV ... incompatible
	/// ComputeBuffer") and dump the URT diagnostic history so the failing buffer and
	/// the sequence of resets/rebuilds that led to it can be identified post-mortem.
	/// Registered on logMessageReceivedThreaded so render-thread errors are caught.
	/// </summary>
	private void OnUnityLogMessage(string condition, string stackTrace, LogType type)
	{
		if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
			return;

		if (string.IsNullOrEmpty(condition))
			return;

		if (condition.IndexOf("missing UAV", StringComparison.OrdinalIgnoreCase) < 0 &&
			condition.IndexOf("incompatible ComputeBuffer", StringComparison.OrdinalIgnoreCase) < 0)
			return;

		// Throttle to one dump per second (thread-safe). CAS ensures only one thread wins.
		long now = System.Diagnostics.Stopwatch.GetTimestamp();
		long prev = Interlocked.Read(ref _lastFaultDumpTicks);
		if (now - prev < System.Diagnostics.Stopwatch.Frequency)
			return;
		if (Interlocked.CompareExchange(ref _lastFaultDumpTicks, now, prev) != prev)
			return; // another thread won the race

		DumpDiagHistory($"GPU dispatch fault: \"{condition}\"");
	}

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
