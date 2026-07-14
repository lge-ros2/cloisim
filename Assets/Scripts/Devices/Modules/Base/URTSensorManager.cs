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
/// (context, acceleration structure, build scratch buffer, scene gathering).
/// Multiple sensors share a single BVH set instead of each building their own.
///
/// Single acceleration structure, fence-gated in place: sensors always trace
/// _rtAccelStruct; it is mutated (gather/transform-update/Build) only when a
/// GraphicsFence confirms no prior-frame trace against it is still in flight
/// (see IsPriorTraceConsumed), and is not offered for tracing again (AccelStruct
/// returns null) until a GraphicsFence confirms its own Build() has actually
/// completed on the GPU (see IsBuildConsumed) — tracing a struct whose Build is
/// still in flight reports near-universal misses, since Build() mutates it in
/// place. Both gates were carried over unchanged from an earlier double-buffered
/// design; a double-buffer (structurally distinct, independently-gathered/built
/// TLAS instances swapped every cycle) reliably avoided any trace-skip window,
/// but two independently built structures for the identical static scene were
/// never guaranteed to be bit-identical, and alternating which one sensors
/// traced flipped grazing-angle hit/miss results every swap — observed as
/// floor-point flicker that worsened with more objects in the scene and could
/// not be fixed by forcing deterministic gather order (the acceleration
/// structure builder itself was not verified bit-reproducible across separate
/// Build() calls with identical input). A single structure cannot disagree with
/// itself, which removes that flicker at the source; the cost is a brief
/// (typically 1-2 frame) trace pause whenever a rebuild is actually needed,
/// instead of the double-buffer's zero-pause-but-occasionally-wrong tracing.
/// The static-scene signature check below (ComputeTransformSignature) exists
/// to keep that pause rare by skipping rebuilds the scene doesn't actually need.
///
/// Per-sensor resources (trace scratch buffer, command buffer, output
/// compute buffers) remain owned by each sensor instance.
/// </summary>
// Execution order is forced LATE so this component's LateUpdate runs *after*
// SensorRenderManager.LateUpdate (default order 0), which is where every URT
// sensor records and submits its trace dispatch for the frame.  The trace
// fence (see IsPriorTraceConsumed) is created in our LateUpdate and must
// capture those already-submitted dispatches, so we must run after them.
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

	/// <summary>
	/// When true, the generation counter increment is deferred until the post-TDR
	/// cooldown expires. Set for non-clean (GPU-timeout) resets so per-sensor resources
	/// are only rebuilt after the GPU has fully recovered, not while it is still in TDR.
	/// </summary>
	private bool _pendingPostTDRGenIncrement = false;

	/// <summary>
	/// True after a TDR gen increment until the first sensor executes a BVH-only GPU submit.
	/// The first sensor that reads this flag via ConsumeBVHWarmup() clears it and submits
	/// a BVH-only CommandBuffer. Subsequent sensors see it as false and dispatch normally.
	/// This is robust against sensors with different update rates: the warmup is consumed
	/// on the first actual render after gen increment, regardless of frame number.
	/// </summary>
	private bool _postTDRBvhWarmupPending = false;

	/// <summary>
	/// Atomically reads and clears _postTDRBvhWarmupPending.
	/// Returns true once (for the first sensor to render after TDR gen increment).
	/// That sensor must execute a BVH-only CommandBuffer submit before returning.
	/// </summary>
	public static bool ConsumeBVHWarmup()
	{
		var inst = s_instance;
		if (inst == null || !inst._postTDRBvhWarmupPending)
			return false;
		inst._postTDRBvhWarmupPending = false;
		return true;
	}

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

	// Cached from LateUpdate (main thread) so DumpDiagHistory can be called safely
	// from FreezeWatchdog's background thread — Time.frameCount throws a
	// UnityException off the main thread, which would otherwise abort the
	// watchdog's escalation before it reaches Process.Kill().
	private volatile int _cachedFrameCount;

	// Same reasoning as _cachedFrameCount: GraphicsBuffer.IsValid()/count/stride
	// call into native code that requires the main thread (IsValidBuffer_Injected),
	// so DumpDiagHistory must never touch _rtBuildScratchBuffer's properties
	// directly when invoked from the watchdog thread.
	private volatile bool _cachedScratchBufferValid;
	private volatile int _cachedScratchBufferCount;
	private volatile int _cachedScratchBufferStride;

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
		// NOTE: this can be called from FreezeWatchdog's background thread on a stall
		// escalation — use the cached frame count, never Time.frameCount directly
		// (throws UnityException off the main thread and would abort the caller
		// before it reaches its own process-kill fallback).
		sb.AppendLine($"[URT-DIAG] === History dump triggered by: {trigger} (frame={inst._cachedFrameCount}) ===");
		sb.AppendLine($"[URT-DIAG] generation={inst._rtAccelStructGeneration}"
			+ $" hasTlas={inst._hasTlas} hasPendingBuild={inst._hasPendingBuild}"
			+ $" accelNull={inst._rtAccelStruct == null}"
			+ $" frameOfLastBuild={inst._frameOfLastBuild}");
		var bs = inst._rtBuildScratchBuffer;
		// NOTE: use the cached values below, never bs.IsValid()/count/stride directly —
		// those call into native code that requires the main thread and throw when
		// invoked from FreezeWatchdog's background thread (same reasoning as frameCount above).
		sb.AppendLine($"[URT-DIAG] buildScratch: "
			+ (bs == null ? "null" : $"hash=0x{bs.GetHashCode():X} valid={inst._cachedScratchBufferValid} count={inst._cachedScratchBufferCount} stride={inst._cachedScratchBufferStride} (cached)")
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

	#region "Single acceleration structure (fence-gated in place)"
	// One acceleration structure, mutated in place and fence-gated on both ends:
	//   - Before mutating (gather/transform-update/Build), IsPriorTraceConsumed()
	//     must confirm no prior-frame trace against it is still in flight (mutation
	//     APIs like AddInstance/RemoveInstance/UpdateInstanceTransform can free/
	//     reallocate underlying GPU buffers a still-executing trace may reference).
	//   - After Build(), IsBuildConsumed() must confirm that GPU work has actually
	//     completed before AccelStruct offers the struct for tracing again — tracing
	//     it earlier (even within the same command buffer as the Build) mutates the
	//     TLAS while GPU work is still reading/writing it, and reports near-universal
	//     misses (NaN) for that frame.
	// Between "Build recorded" and "build confirmed complete", AccelStruct returns
	// null and sensors skip that frame's dispatch (reusing the previous output) —
	// see the class doc comment for why this trades a rare pause for the flicker a
	// two-structure design could not otherwise avoid.

	private IRayTracingAccelStruct _rtAccelStruct;

	private readonly List<InstanceEntry> _instances = new();
	private readonly HashSet<InstanceKey> _desiredKeys = new();
	private readonly HashSet<InstanceKey> _existingKeys = new();
	private float _lastGatherTime;
	private bool _dirty = true;

	// Aggregate signature of all instances' world transforms, used to skip a
	// rebuild (and the trace-pause it costs) when nothing actually changed. NaN
	// forces a rebuild (never skipped) on the next check — used whenever the
	// scene is known to be in flux (dirty) so a real change is never masked.
	private double _lastStaticSignature = double.NaN;

	// Whether the struct has ever been successfully built (i.e. Build() was called
	// with non-empty instances) AND that build has been confirmed complete.
	// Prevents AccelStruct from returning a struct whose m_TopLevelAccelStruct is
	// still null or whose Build is still in flight, either of which would cause
	// downstream Bind() calls to pass null/inconsistent UAV buffers and trigger a
	// "missing UAV" GPU error.
	private bool _hasTlas;

	// Trace fence: covers all trace dispatches that read _rtAccelStruct. Before
	// mutating the struct we check this to ensure the GPU has finished all
	// previous traces that read it.
	private static readonly bool s_graphicsFenceSupported = SystemInfo.supportsGraphicsFence;
	private GraphicsFence _traceFence;
	private bool _hasTraceFence;
	// No-fence fallback: frame number of the most recent trace.
	private int _frameOfLastTrace = -1;

	// Build-completion fence: the struct must not be offered for tracing again
	// until its most recent Build() GPU work is confirmed complete (see the
	// region comment above and IsBuildConsumed's doc comment).
	private GraphicsFence _buildFence;
	private bool _hasBuildFence;
	// No-fence fallback: frame number of the most recent Build() recorded.
	private int _frameOfLastBuildForStruct = -1;

	// True from the frame a Build() is recorded until IsBuildConsumed() confirms
	// it finished. While true, EnsureBVHReady does nothing but poll that fence —
	// no new mutation is attempted, and AccelStruct returns null.
	private bool _hasPendingBuild;

	// Set when EnsureBVHReady determines sensors will trace the struct this
	// frame; captured in LateUpdate once that frame's command buffer(s) are
	// submitted, refreshing the trace fence.
	private bool _pendingFenceNeeded;

	// Set when EnsureBVHReady records a Build(); captured in LateUpdate once that
	// frame's command buffer (containing the Build dispatch) has been submitted.
	private bool _pendingBuildFenceNeeded;
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
		public Renderer renderer;      // MeshRenderer or SkinnedMeshRenderer
		public Mesh keyMesh;           // identity mesh used for gather diffing (sharedMesh)
		public Mesh geometryMesh;      // mesh handed to the accel struct (baked snapshot for skinned)
		public int subMeshIndex;
		public int handle;

		// Non-null => animated skin: re-bake geometryMesh and rebuild its BLAS.
		public SkinnedMeshRenderer skinned;

		// Skin refresh throttling / idle detection (only meaningful when skinned != null).
		public float nextBakeTime;     // realtime after which a re-bake is allowed
		public Bounds lastBakedBounds; // world AABB at the last actual bake; used to skip idle actors
	}

	private readonly struct InstanceKey : IEquatable<InstanceKey>
	{
		private readonly EntityId _rendererId;
		private readonly EntityId _meshId;
		private readonly int _subMeshIndex;

		public InstanceKey(Renderer renderer, Mesh mesh, int subMeshIndex)
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

	/// <summary>
	/// Stable per-link identifier stored in MeshInstanceDesc.instanceID so a lidar's
	/// ray-trace shader can recognize hits against its own mounting LINK (self-hit)
	/// and re-trace past them (see LidarRayTrace.compute / Lidar.SetSensorPoseParams).
	///
	/// Scoped to the nearest SDF.Helper.Link ancestor, not the whole robot model:
	/// a lidar only physically grazes the single link it is bolted to (e.g. its
	/// mount bracket), and other links of the same robot (arms, other sensors'
	/// brackets, ...) are legitimate ray-trace targets. Excluding the entire model
	/// discarded those as false self-hits, which got worse as more instances were
	/// added to the scene (more BVH depth meant more legitimately-close hits
	/// misidentified as self across unrelated links).
	///
	/// transform.root is NOT usable as a fallback identity here: every SDF-imported
	/// model (robots, ground plane, props, ...) is parented under the single shared
	/// "World" GameObject (see Main.cs / Import.World.cs), so transform.root is
	/// identical for the whole scene. Falls back to the outermost Helper.Model
	/// (Helper.Base.RootModel) when no Link is found (e.g. a single-link model with
	/// no Helper.Link, or non-SDF geometry falls back further to transform.root).
	///
	/// IDs are assigned from a private monotonic counter (see s_selfExclusionIds),
	/// NOT derived by truncating EntityId.ToULong() to 32 bits: EntityId is 64-bit
	/// specifically because Unity's legacy 32-bit instance IDs can collide/overflow
	/// as more objects are created in a session, so truncating it back to 32 bits
	/// reintroduces that exact collision risk. A collision here would tag an
	/// unrelated object (e.g. the floor) with the same ID as a lidar's own link,
	/// making the shader wrongly retrace/discard legitimate floor hits — observed
	/// as floor points flickering once enough scene objects pushed two EntityIds
	/// into the same truncated value, and persisting even after those objects
	/// stopped moving (a fixed misidentification, not a timing artifact).
	/// </summary>
	private static readonly Dictionary<Transform, uint> s_selfExclusionIds = new();
	private static uint s_nextSelfExclusionId = 1; // 0 reserved as "unset"; MeshInstanceDesc's own default sentinel is 0xFFFFFFFF

	public static uint SelfExclusionIdOf(Component component)
	{
		var linkHelper = component.GetComponentInParent<SDFormat.Helper.Link>();
		Transform scopeTransform;
		if (linkHelper != null)
		{
			scopeTransform = linkHelper.transform;
		}
		else
		{
			var baseHelper = component.GetComponentInParent<SDFormat.Helper.Base>();
			scopeTransform = (baseHelper != null && baseHelper.RootModel != null)
				? baseHelper.RootModel.transform
				: component.transform.root;
		}

		if (!s_selfExclusionIds.TryGetValue(scopeTransform, out var id))
		{
			id = s_nextSelfExclusionId++;
			s_selfExclusionIds[scopeTransform] = id;
		}
		return id;
	}

	private const float MinSceneGatherInterval = 10.0f;
	private const float RendererCountScalingFactor = 500.0f;

	[SerializeField]
	private float _sceneGatherInterval = MinSceneGatherInterval;

	// Skin (actor) BVH refresh rate. Re-baking a SkinnedMeshRenderer and rebuilding its
	// BLAS every render frame is expensive; animation only needs ~30 Hz to look correct
	// to a sensor, so throttle it independently of the render frame rate.
	[SerializeField]
	private float _skinRefreshRate = 30.0f;

	// Squared world-space AABB delta below which an actor is treated as idle (no re-bake).
	private const float SkinIdleBoundsSqrEpsilon = 1e-8f;

	private int _cullingMask;

	/// <summary>
	/// Cameras registered for shared BVH access.
	/// </summary>
	private readonly HashSet<EntityId> _registeredCameras = new();

	[SerializeField]
	private int _frameOfLastBuild = -1;

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
	/// The current acceleration structure. All sensors trace against this.
	/// Returns null when the struct has never been built, when a Build() has
	/// been recorded but not yet confirmed complete (see IsBuildConsumed), or
	/// when it currently holds zero instances — binding a struct with an empty
	/// bottom-level BVH list still passes IsBuildConsumed/_hasTlas checks (those
	/// only gate GPU completion, not geometry presence) but produces a
	/// "bottomBvhs ... not set" kernel warning on Dispatch, since the shader
	/// still declares that property and nothing populated it. Any of these
	/// cases would otherwise pass null/inconsistent/empty buffers to the GPU.
	/// </summary>
	public static IRayTracingAccelStruct AccelStruct
	{
		get
		{
			var inst = Instance;
			if (inst == null || !inst._hasTlas || inst._instances.Count == 0)
				return null;
			return inst._rtAccelStruct;
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

		if (inst._rtAccelStruct == null)
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
			s_instance._dirty = true;
		}
	}

	/// <summary>
	/// Ensure the scene BVH is up-to-date for this frame.
	/// Called by each URT sensor at the start of its render method.
	/// Only the first caller per frame does actual work; subsequent
	/// calls in the same frame are no-ops (they use the same struct via
	/// AccelStruct).
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
			return; // Already processed this frame

		// Post-reset cooldown: GPU may still be recovering from a dispatch error
		// (WaitForGPUQuiescence timed out). Skipping keeps AccelStruct null
		// so sensors skip dispatch — prevents "missing UAV" on the first post-rebuild trace.
		if (currentFrame < inst._postResetResumeBuildFrame)
			return;

		inst._frameOfLastBuild = currentFrame;

		// A Build() was recorded on an earlier call; nothing to do here but poll
		// whether its GPU work has actually completed. Until it has, the struct's
		// TLAS is being mutated in place and must not be traced (see IsBuildConsumed's
		// doc comment) or mutated further — return either way, once per frame.
		if (inst._hasPendingBuild)
		{
			if (inst.IsBuildConsumed())
			{
				inst._hasTlas = true;
				inst._hasPendingBuild = false;
				// Let it be traced THIS frame (fence bookkeeping below) before ever
				// considering another rebuild — see the class doc comment on why a
				// single structure must not be re-mutated in the same cycle it was
				// just confirmed ready, or it would never actually get traced.
				inst._pendingFenceNeeded = true;
				inst._frameOfLastTrace = currentFrame;
			}
			return;
		}

		// Struct is stable (no build in flight). If it has ever been built, this
		// frame's sensors are about to trace it — refresh the trace fence bookkeeping.
		if (inst._hasTlas)
		{
			inst._pendingFenceNeeded = true;
			inst._frameOfLastTrace = currentFrame;
		}

		// In-flight protection: if the GPU is still processing a prior frame's
		// trace against this struct, don't mutate it yet. Sensors keep tracing
		// the same (unchanged, still valid) struct — at most 1 frame stale,
		// never accumulating, and never wrong (it's the only struct, so there is
		// no "other, different" version to disagree with).
		if (!inst.IsPriorTraceConsumed())
			return;

		// --- Detect new/removed objects ---
		var realtimeNow = Time.realtimeSinceStartup;

		if (inst._dirty ||
			(realtimeNow - inst._lastGatherTime > inst._sceneGatherInterval))
		{
			inst.GatherSceneMeshes();
			inst._dirty = false;
		}

		inst.UpdateInstanceTransforms();

		// Skip Build when no geometry is present. Re-set dirty so GatherSceneMeshes
		// re-runs next frame instead of waiting for the 10-second interval (covers
		// the early-loading window). _hasTlas must also be cleared here: the loop
		// above already RemoveInstance()'d every entry (CPU-side bottomBvhs list is
		// now empty) without a matching Build(), so a stale _hasTlas=true from a
		// previous build would keep offering this now-empty struct via AccelStruct,
		// and binding it produces "_AccelStructbottomBvhs ... not set" kernel warnings.
		if (inst._instances.Count == 0)
		{
			inst._hasTlas = false;
			inst._dirty = true;
			return;
		}

		// --- Skip the rebuild (and the trace-pause it would cost) when nothing
		// actually changed since the last build. Without this, a single structure
		// would need to rebuild-and-fence-wait every cycle forever (matching the
		// original double-buffer's unconditional per-cycle Build()), pausing
		// sensor output constantly even for a scene that never moves. ---
		var sig = inst.ComputeTransformSignature();
		if (inst._hasTlas && sig == inst._lastStaticSignature)
			return; // unchanged — current TLAS remains valid, sensors keep tracing it
		inst._lastStaticSignature = sig;

		CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:BuildAS");
		using (s_BuildBVHMarker.Auto())
		{
			// --- Resize scratch buffer if needed ---
			var scratchHashBefore = inst._rtBuildScratchBuffer?.GetHashCode() ?? 0;
			var instanceCountNow = inst._instances.Count;

			inst._rtBuildScratchBuffer = GrowScratch(
				inst._rtBuildScratchBuffer, inst._rtAccelStruct.GetBuildScratchBufferRequiredSizeInBytes());

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

			// --- Build BVH in place ---
			inst._rtAccelStruct.Build(cmd, inst._rtBuildScratchBuffer);
		}

		// The struct now holds a freshly-recorded Build(), but that GPU work has
		// not been confirmed complete yet. Mark it pending and stop offering the
		// struct for tracing (AccelStruct returns null) until IsBuildConsumed()
		// reports the build fence has passed on a later call.
		inst._hasTlas = false;
		inst._hasPendingBuild = true;
		inst._pendingBuildFenceNeeded = true;
		inst._frameOfLastBuildForStruct = currentFrame;
		}
	}

	/// <summary>
	/// True when the GPU has finished every prior trace dispatch that reads
	/// _rtAccelStruct, so it is safe to mutate (FreeTopLevelAccelStruct will be
	/// called internally by AddInstance / UpdateInstanceTransform / etc.).
	/// Uses a GraphicsFence (refreshed every frame the struct is traced) when
	/// supported; falls back to a frame-count gap otherwise.
	/// </summary>
	private bool IsPriorTraceConsumed()
	{
		if (s_graphicsFenceSupported)
			return !_hasTraceFence || _traceFence.passed;

		var prevFrame = _frameOfLastTrace;
		return prevFrame < 0 || Time.frameCount - prevFrame >= ScratchSafetyFrames;
	}

	/// <summary>
	/// True once the Build() GPU work most recently recorded for _rtAccelStruct
	/// has actually completed, so it is safe to offer the struct for tracing again.
	/// Before an equivalent gate existed in the double-buffered predecessor of this
	/// class, EnsureBVHReady let sensors trace a struct immediately after recording
	/// Build() into the same command buffer as that frame's ray-trace dispatch;
	/// sensors then traced a TLAS whose build had not finished on the GPU, which
	/// TraceRayClosestHit reports as near-universal ray misses (NaN) — observed as
	/// ranges alternating between a normal scan and an almost-all-NaN scan every
	/// other frame.
	///
	/// This gate is the primary mitigation but not a complete guarantee across all
	/// scenes/sessions (see Lidar.TryProcessStandardData's comment for the client-side
	/// second line of defense that catches whatever still slips through here).
	/// </summary>
	private bool IsBuildConsumed()
	{
		if (s_graphicsFenceSupported)
			return _hasBuildFence && _buildFence.passed;

		var builtFrame = _frameOfLastBuildForStruct;
		return builtFrame >= 0 && Time.frameCount > builtFrame;
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

	/// <summary>
	/// Adapts an arbitrary teardown action (e.g. RenderTexture.Release()+Destroy(),
	/// ComputeShader Destroy()) to the IDisposable-based deferred free queue below.
	/// </summary>
	private sealed class ActionDisposable : IDisposable
	{
		private readonly Action _action;
		public ActionDisposable(Action action) => _action = action;
		public void Dispose() => _action?.Invoke();
	}

	/// <summary>
	/// Same contract as DeferDispose(IDisposable), for resources that are not
	/// IDisposable (RenderTexture, ComputeShader) and must instead be torn down
	/// via Destroy()/Release() once the GPU has caught up.
	/// </summary>
	public static void DeferDispose(Action teardownAction) =>
		DeferDispose(new ActionDisposable(teardownAction));

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
		_cachedFrameCount = Time.frameCount;
		var scratch = _rtBuildScratchBuffer;
		_cachedScratchBufferValid = scratch != null && scratch.IsValid();
		_cachedScratchBufferCount = scratch != null && scratch.IsValid() ? scratch.count : 0;
		_cachedScratchBufferStride = scratch != null && scratch.IsValid() ? scratch.stride : 0;

		// Consume a fault flagged by OnUnityLogMessage (render/background thread) and
		// actually recover: rebuild the shared AccelStruct and bump the per-sensor
		// rebuild generation, same as an explicit reset. ResetSceneInternal() has its
		// own GPU-quiescence handling, so it is safe to call unconditionally here even
		// if the GPU is still recovering from a TDR.
		if (_pendingFaultRecovery)
		{
			_pendingFaultRecovery = false;
			Debug.LogWarning("[URTSensorManager] GPU dispatch fault detected — auto-recovering by rebuilding URT resources.");
			ResetSceneInternal();
		}

		// Log when cooldown expires so the log shows exactly when sensors resume tracing.
		if (_postResetResumeBuildFrame > 0 && Time.frameCount == _postResetResumeBuildFrame)
		{
			Debug.Log("[URTSensorManager] Post-reset cooldown expired — BVH rebuild and sensor tracing resuming.");

			// Deferred gen increment for TDR resets: do this NOW (after GPU recovery)
			// so per-sensor rebuild allocates fresh GPU buffers on a healthy GPU.
			// If gen were incremented at reset time (frame N), rebuild would fire at
			// frame N+1 (GPU still in TDR recovery) and produce invalid buffer handles
			// that cause "missing UAV ID XXXX" + Xid 109 at the first dispatch (N+60).
			if (_pendingPostTDRGenIncrement)
			{
				_rtAccelStructGeneration++;
				_pendingPostTDRGenIncrement = false;
				// Arm the BVH warmup flag. The first sensor to render will call ConsumeBVHWarmup(),
				// execute a BVH-only CommandBuffer submit, and clear the flag. Subsequent sensors
				// (same or later frame) see the flag cleared and dispatch normally. This is
				// robust against sensors with different update rates, unlike the previous
				// frame-count window which could expire before slower sensors first rendered.
				_postTDRBvhWarmupPending = true;
				Debug.Log($"[URTSensorManager] Post-TDR gen increment → {_rtAccelStructGeneration}. "
					+ "BVH warmup pending — first sensor to render will submit BVH-only, then dispatch resumes.");
			}
		}

		// Bracket the drain so a stall inside a native Dispose() call (freeing a
		// buffer the GPU turns out to still be using) is attributed to this stage
		// instead of showing up as the misleading default "idle" in FreezeWatchdog
		// diagnostics.
		CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:DeferredFree");
		DrainDeferredScratchFrees();
		DrainDeferredDisposes();
		CLOiSim.Diagnostics.FreezeWatchdog.Mark("idle");

		// Runs after SensorRenderManager.LateUpdate, so every URT trace dispatch
		// for this frame is already submitted. Capture a fence covering this
		// frame's trace(s) against the struct.
		if (_pendingFenceNeeded && s_graphicsFenceSupported)
		{
			_traceFence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
			_hasTraceFence = true;
		}
		_pendingFenceNeeded = false;

		// Capture a fence covering this frame's Build() dispatch (if any), so the
		// check in EnsureBVHReady can confirm it has actually completed before the
		// struct is offered for tracing again.
		if (_pendingBuildFenceNeeded && s_graphicsFenceSupported)
		{
			_buildFence = Graphics.CreateGraphicsFence(
				GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);
			_hasBuildFence = true;
		}
		_pendingBuildFenceNeeded = false;
	}

	private void ReleaseResources()
	{
		DrainDeferredScratchFrees(force: true);
		DrainDeferredDisposes(force: true);

		_rtBuildScratchBuffer?.Dispose();
		_rtBuildScratchBuffer = null;

		// Drop stale Transform->ID mappings so destroyed models don't keep their
		// Transforms rooted in this dictionary forever (see SelfExclusionIdOf).
		s_selfExclusionIds.Clear();

		_rtAccelStruct?.Dispose();
		_rtAccelStruct = null;
		DestroyOwnedGeometry();
		_instances.Clear();
		_existingKeys.Clear();
		_desiredKeys.Clear();
		_lastGatherTime = 0f;
		_dirty = true;
		_hasTraceFence = false;
		_frameOfLastTrace = -1;
		_hasTlas = false;
		_hasBuildFence = false;
		_frameOfLastBuildForStruct = -1;

		_rtContext?.Dispose();
		_rtContext = null;

		_frameOfLastBuild = -1;
		_pendingFenceNeeded = false;
		_hasPendingBuild = false;
		_pendingBuildFenceNeeded = false;
		_postResetResumeBuildFrame = 0;
		_lastStaticSignature = double.NaN;

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
			// Probe the GPU with a bounded fence poll BEFORE AsyncGPUReadback.WaitAllRequests().
			// WaitAllRequests() has no timeout and cannot be interrupted: on a wedged GPU
			// (TDR / GSP-death / Xid 109) the readback whose dispatch faulted never completes,
			// so the call — and the whole main thread — would hang forever. The fence cannot be
			// timed out either, but we CAN decline to enter the blocking drain: if the fence does
			// not pass within the deadline we treat the GPU as lost and skip WaitAllRequests().
			if (s_graphicsFenceSupported)
			{
				var fence = Graphics.CreateGraphicsFence(
					GraphicsFenceType.CPUSynchronisation, SynchronisationStageFlags.AllGPUOperations);

				var deadline = System.Diagnostics.Stopwatch.GetTimestamp()
					+ System.Diagnostics.Stopwatch.Frequency; // 1-second timeout
				while (!fence.passed)
				{
					if (System.Diagnostics.Stopwatch.GetTimestamp() > deadline)
					{
						Debug.LogWarning("[URTSensorManager] WaitForGPUQuiescence: timed out after 1s — GPU may be lost; skipping blocking readback drain and applying extended rebuild cooldown.");
						return false;
					}
					System.Threading.Thread.Sleep(1);
				}
			}

			// GPU is responsive (or graphics fences unsupported): completing readbacks is now fast.
			AsyncGPUReadback.WaitAllRequests();
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
		if (_rtAccelStruct == null) return;

		// Wait for in-flight GPU dispatches to complete before freeing resources.
		var gpuClean = WaitForGPUQuiescence();

		// Force-free GPU-referenced resources ONLY when the GPU actually quiesced.
		// On a timeout the GPU may still be reading these buffers, so an immediate
		// Dispose() is a use-after-free that re-faults the driver ("missing UAV" /
		// Xid 109). When not clean, route the build scratch buffer through the
		// fence-gated deferred path and leave the deferred queues to drain on a
		// later clean reset (or leak harmlessly if the device never recovers).
		if (gpuClean)
		{
			CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:DeferredFree");
			DrainDeferredScratchFrees(force: true);
			DrainDeferredDisposes(force: true);
			_rtBuildScratchBuffer?.Dispose();
			_rtBuildScratchBuffer = null;
			CLOiSim.Diagnostics.FreezeWatchdog.Mark("idle");
		}
		else
		{
			DeferScratchFree(_rtBuildScratchBuffer);
			_rtBuildScratchBuffer = null;
		}

		if (!gpuClean)
		{
			// GPU TDR or timeout: the RayTracingContext itself may hold invalidated
			// GPU device handles (Vulkan VkBuffer IDs, D3D12 resource pointers).
			// Reusing the same context after TDR can produce "missing input compute
			// buffer" on the first Build/Dispatch even after the cooldown, because
			// the context's internal geometry-pool buffers reference old device handles.
			// Full reconstruction from scratch gives the driver a completely clean slate.
			Debug.LogWarning("[URTSensorManager] GPU timeout — rebuilding RayTracingContext from scratch.");
			_rtAccelStruct?.Dispose();
			_rtAccelStruct = null;
			_rtContext?.Dispose();
			_rtContext = null;
			Initialize();
		}
		else
		{
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
			}
		}

		s_selfExclusionIds.Clear();

		DestroyOwnedGeometry();
		_instances.Clear();
		_existingKeys.Clear();
		_desiredKeys.Clear();
		_lastGatherTime = 0f;
		_dirty = true;
		_hasTraceFence = false;
		_frameOfLastTrace = -1;
		_hasTlas = false;
		_hasBuildFence = false;
		_frameOfLastBuildForStruct = -1;
		_lastStaticSignature = double.NaN;

		_frameOfLastBuild = -1;
		_pendingFenceNeeded = false;
		_hasPendingBuild = false;
		_pendingBuildFenceNeeded = false;
		_diagPrevInstanceCount = -1;
		_postTDRBvhWarmupPending = false;

		// Signal per-sensor consumers (DepthCamera, Lidar) that they must recreate
		// their per-sensor shader wrapper so no stale binding to the disposed
		// accel structs remains.
		//
		// For a clean reset the GPU is quiescent: increment immediately so per-sensor
		// rebuild fires at frame N+1 (safe, GPU is healthy).
		//
		// For a non-clean (TDR) reset: defer the increment until the cooldown expires
		// (frame N+60). Incrementing now would trigger per-sensor rebuild at frame N+1
		// while the GPU is still in TDR recovery, producing invalid buffer handles.
		// Those handles cause "missing UAV ID XXXX" + Xid 109 at the first dispatch.
		if (gpuClean)
		{
			_rtAccelStructGeneration++;
			_pendingPostTDRGenIncrement = false;
		}
		else
		{
			_pendingPostTDRGenIncrement = true;
		}

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

	/// <summary>
	/// Destroy the owned baked-mesh snapshots held by skinned instances, so
	/// clearing the instance list does not leak the Meshes created by
	/// GatherSceneMeshes. Static-mesh entries reference shared meshes and are left alone.
	/// </summary>
	private void DestroyOwnedGeometry()
	{
		for (var i = 0; i < _instances.Count; i++)
		{
			var entry = _instances[i];
			if (entry.skinned != null && entry.geometryMesh != null)
				Destroy(entry.geometryMesh);
		}
	}

	#region "Scene Gathering"

	/// <summary>
	/// Gather all active MeshRenderers that match the culling mask and populate
	/// _rtAccelStruct. The instance list is refreshed (diffed against the current
	/// live-renderer set) from scratch via this method.
	/// </summary>
	private void GatherSceneMeshes()
	{
		using (s_GatherSceneMeshesMarker.Auto())
		{
		var accelStruct = _rtAccelStruct;
		if (accelStruct == null)
			return;

		var instances = _instances;
		var existingKeys = _existingKeys;
		var desiredKeys = _desiredKeys;

		existingKeys.Clear();
		for (var i = 0; i < instances.Count; i++)
		{
			var entry = instances[i];
			existingKeys.Add(new InstanceKey(entry.renderer, entry.keyMesh, entry.subMeshIndex));
		}

		desiredKeys.Clear();

		// Deterministic order (sorted by EntityId below): FindObjectsByType's order
		// is otherwise implementation-defined. With a single acceleration structure
		// this no longer affects correctness (there is only one structure to be
		// internally consistent with itself), but a stable add order still makes
		// diagnostics/instance-count logs reproducible across runs. Left in place
		// from when this mattered more, back when two independently-built
		// structures were compared against each other. (FindObjectsSortMode is
		// obsolete on this Unity version, so we sort manually by EntityId instead.)
		var renderers = FindObjectsByType<MeshRenderer>();
		var skinnedRenderers = FindObjectsByType<SkinnedMeshRenderer>();
		Array.Sort(renderers, (a, b) => EntityId.ToULong(a.GetEntityId()).CompareTo(EntityId.ToULong(b.GetEntityId())));
		Array.Sort(skinnedRenderers, (a, b) => EntityId.ToULong(a.GetEntityId()).CompareTo(EntityId.ToULong(b.GetEntityId())));

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
						instanceID = SelfExclusionIdOf(renderer),
						enableTriangleCulling = false,
						opaqueGeometry = true
					};

					var handle = accelStruct.AddInstance(desc);
					instances.Add(new InstanceEntry
					{
						renderer = renderer,
						keyMesh = mesh,
						geometryMesh = mesh,
						subMeshIndex = sub,
						handle = handle,
						skinned = null
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

		// Skinned (animated) meshes — actors. These are not caught by the MeshRenderer
		// pass above; their deformed geometry lives in a SkinnedMeshRenderer and must be
		// snapshotted with BakeMesh into an owned Mesh. UpdateInstanceTransforms re-bakes
		// and rebuilds this instance's BLAS every frame so the animation is traced.
		foreach (var smr in skinnedRenderers)
		{
			if (!smr.gameObject.scene.isLoaded)
				continue;

			if (!smr.enabled || !smr.gameObject.activeInHierarchy)
				continue;

			if ((_cullingMask & (1 << smr.gameObject.layer)) == 0)
			{
				skippedLayer++;
				continue;
			}

			var srcMesh = smr.sharedMesh;
			if (srcMesh == null)
			{
				skippedNoMesh++;
				continue;
			}

			for (int sub = 0; sub < srcMesh.subMeshCount; sub++)
			{
				var key = new InstanceKey(smr, srcMesh, sub);
				desiredKeys.Add(key);

				if (existingKeys.Contains(key))
					continue;

				Mesh bakedMesh = null;
				try
				{
					bakedMesh = new Mesh { name = $"{smr.name}_urtBaked_{sub}" };
					smr.BakeMesh(bakedMesh);

					var desc = new MeshInstanceDesc(bakedMesh, sub)
					{
						localToWorldMatrix = smr.localToWorldMatrix,
						mask = 0xFF,
						instanceID = SelfExclusionIdOf(smr),
						enableTriangleCulling = false,
						opaqueGeometry = true
					};

					var handle = accelStruct.AddInstance(desc);
					instances.Add(new InstanceEntry
					{
						renderer = smr,
						keyMesh = srcMesh,
						geometryMesh = bakedMesh,
						subMeshIndex = sub,
						handle = handle,
						skinned = smr
					});
					addedCount++;
				}
				catch (Exception e)
				{
					skippedError++;
					if (bakedMesh != null)
						Destroy(bakedMesh);
					Debug.LogWarning($"[URTSensorManager] Failed to add skinned '{smr.name}' sub={sub} " +
						$"mesh='{srcMesh.name}' verts={srcMesh.vertexCount}: {e.Message}");
				}
			}
		}

		for (var i = instances.Count - 1; i >= 0; i--)
		{
			var entry = instances[i];
			var key = new InstanceKey(entry.renderer, entry.keyMesh, entry.subMeshIndex);
			if (desiredKeys.Contains(key))
				continue;

			try
			{
				accelStruct.RemoveInstance(entry.handle);
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Failed to remove '{entry.renderer?.name ?? "<destroyed>"}' " +
					$"sub={entry.subMeshIndex} mesh='{entry.keyMesh?.name ?? "<unknown>"}': {e.Message}");
			}

			// Free the owned baked-mesh snapshot for skinned instances.
			if (entry.skinned != null && entry.geometryMesh != null)
				Destroy(entry.geometryMesh);

			instances.RemoveAt(i);
			removedCount++;
		}

		_lastGatherTime = Time.realtimeSinceStartup;

		var totalRenderers = renderers.Length + skinnedRenderers.Length;
#if UNITY_EDITOR
		Debug.Log($"[URTSensorManager] GatherSceneMeshes: added={addedCount}, removed={removedCount}, " +
			$"skippedLayer={skippedLayer}, skippedNoMesh={skippedNoMesh}, " +
			$"skippedError={skippedError}, totalRenderers={totalRenderers} (skinned={skinnedRenderers.Length})");
#endif
		_sceneGatherInterval = MinSceneGatherInterval + (float)totalRenderers / RendererCountScalingFactor;

		// Allocate / resize the build scratch buffer
		_rtBuildScratchBuffer = GrowScratch(
			_rtBuildScratchBuffer, accelStruct.GetBuildScratchBufferRequiredSizeInBytes());
		}
	}

	// Quantization step (meters / quaternion units) applied before hashing transforms
	// in ComputeTransformSignature. Physics/balance controllers rarely settle to
	// EXACTLY zero residual motion even when a scene looks static, so comparing raw
	// float positions frame-to-frame almost never reports "unchanged" — the guard
	// then never engages and the flicker it exists to fix persists. Measured via
	// diagnostic logging: a spawned prop that visually looked fully at rest was
	// still drifting 1-8mm per ~15-frame check (residual physics jitter, e.g. from
	// having settled against another prop) — well above an initial 0.2mm quantum.
	// 5mm is below anything visually noticeable for a prop at rest, while still
	// catching real repositioning.
	private const float StaticSignatureQuantum = 0.005f;

	/// <summary>
	/// Cheap aggregate signature of every instance's world position/rotation,
	/// quantized to StaticSignatureQuantum. Used only to skip a rebuild (and the
	/// trace-pause it costs) when nothing moved enough to matter — any instance
	/// moving beyond the quantum (or being added/removed, which changes the
	/// live-renderer set) shifts the value, forcing a rebuild. Combines quantized
	/// integer components via a standard hash-combine so the result depends only
	/// on quantized values, never on raw float noise below the quantum.
	/// </summary>
	private double ComputeTransformSignature()
	{
		var instances = _instances;
		unchecked
		{
			long hash = 17;
			hash = hash * 31 + instances.Count;
			for (var i = 0; i < instances.Count; i++)
			{
				var r = instances[i].renderer;
				if (r == null)
					continue;
				var t = r.transform;
				var p = t.position;
				var q = t.rotation;
				hash = hash * 31 + Quantize(p.x);
				hash = hash * 31 + Quantize(p.y);
				hash = hash * 31 + Quantize(p.z);
				hash = hash * 31 + Quantize(q.x);
				hash = hash * 31 + Quantize(q.y);
				hash = hash * 31 + Quantize(q.z);
				hash = hash * 31 + Quantize(q.w);
			}
			return hash;
		}
	}

	private static long Quantize(float v)
	{
		return (long)Mathf.Round(v / StaticSignatureQuantum);
	}

	/// <summary>
	/// Update world-space transforms for all cached instances in _rtAccelStruct.
	/// </summary>
	private void UpdateInstanceTransforms()
	{
		using (s_UpdateInstanceTransformsMarker.Auto())
		{
		var accelStruct = _rtAccelStruct;
		if (accelStruct == null)
			return;

		var instances = _instances;
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

				// Free the owned baked-mesh snapshot for skinned instances.
				if (entry.skinned != null && entry.geometryMesh != null)
					Destroy(entry.geometryMesh);

				instances.RemoveAt(i);
				needsRebuild = true;
				continue;
			}

			if (entry.skinned != null)
			{
				// Animated skin: snapshot the current deformed geometry and force a BLAS
				// rebuild. The compute backend caches each BLAS by mesh and only builds it
				// once (bvhBuilt), so a plain transform update would freeze the actor in its
				// first-frame pose. Remove+Add drops the stale BLAS and re-adds the same
				// (re-baked) mesh, which rebuilds its BLAS from fresh vertices on the next Build.
				//
				// This is costly, so it is throttled to _skinRefreshRate (default 30 Hz) and
				// skipped entirely while the actor is idle (world AABB unchanged). Between
				// refreshes the previously-built BLAS/transform is kept as-is.
				var now = Time.realtimeSinceStartup;
				if (now < entry.nextBakeTime)
					continue;

				var interval = (_skinRefreshRate > 0f) ? (1f / _skinRefreshRate) : 0f;
				entry.nextBakeTime = now + interval;

				var bounds = entry.skinned.bounds;
				var centerDelta = bounds.center - entry.lastBakedBounds.center;
				var extentsDelta = bounds.extents - entry.lastBakedBounds.extents;
				if (centerDelta.sqrMagnitude < SkinIdleBoundsSqrEpsilon &&
					extentsDelta.sqrMagnitude < SkinIdleBoundsSqrEpsilon)
				{
					// Idle actor: nothing moved since the last bake — keep the existing BLAS.
					instances[i] = entry;
					continue;
				}

				try
				{
					entry.skinned.BakeMesh(entry.geometryMesh);

					accelStruct.RemoveInstance(entry.handle);

					var desc = new MeshInstanceDesc(entry.geometryMesh, entry.subMeshIndex)
					{
						localToWorldMatrix = entry.skinned.localToWorldMatrix,
						mask = 0xFF,
						instanceID = SelfExclusionIdOf(entry.skinned),
						enableTriangleCulling = false,
						opaqueGeometry = true
					};
					entry.handle = accelStruct.AddInstance(desc);
					entry.lastBakedBounds = bounds;
				}
				catch (Exception e)
				{
					Debug.LogWarning($"[URTSensorManager] Failed to update skinned instance " +
						$"'{entry.renderer.name}' sub={entry.subMeshIndex}: {e.Message}");
				}
				instances[i] = entry;
				continue;
			}

			accelStruct.UpdateInstanceTransform(entry.handle, entry.renderer.localToWorldMatrix);
		}

		if (needsRebuild)
		{
			_lastGatherTime = 0f; // Force re-gather next call
		}
		}
	}

	#endregion

	#region "Unity lifecycle"

	// Throttle: timestamp of last fault dump (Stopwatch ticks). Thread-safe via
	// Interlocked — logMessageReceivedThreaded fires from the render thread, not main.
	private long _lastFaultDumpTicks = long.MinValue;

	// Set by OnUnityLogMessage (render/background thread) when a "missing UAV" /
	// "incompatible ComputeBuffer" fault is detected, consumed by LateUpdate (main
	// thread) to actually recover. Without this, the fault was only ever logged —
	// the corrupted shared AccelStruct and per-sensor buffers were left in place and
	// every sensor kept re-dispatching against them every frame, producing continuous
	// garbled/striped sensor output instead of one glitch that self-heals.
	private volatile bool _pendingFaultRecovery;

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

		// Detection alone does not recover anything: ResetScene() (the only path that
		// rebuilds the shared AccelStruct and bumps the per-sensor rebuild generation)
		// only ran from the explicit Ctrl+R / WebSocket reset. Flag it here so the next
		// LateUpdate (main thread) rebuilds the GPU resources instead of leaving every
		// sensor dispatching against the now-faulted buffers indefinitely.
		_pendingFaultRecovery = true;
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
