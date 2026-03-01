/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using Segmentation;
using Debug = UnityEngine.Debug;

namespace SensorDevices
{
	/// <summary>
	/// Manages a shared Unified Ray Tracing acceleration structure for all sensor
	/// devices (lidar, depth camera, segmentation camera).
	///
	/// Uses the Unified RT API which provides:
	///   - Hardware RT core path when available
	///   - Compute BVH fallback (RadeonRays) on any GPU with compute shaders
	///
	/// Each MeshInstanceDesc carries:
	///   - instanceID: segmentation class ID
	///   - mask: 8-bit mask for lidar self-exclusion
	///
	/// Rebuilt every frame in LateUpdate to track dynamic objects.
	/// </summary>
	public class URTSensorManager : MonoBehaviour
	{
		public static URTSensorManager Instance { get; private set; }

		// ── Profiling markers ──
		private static readonly ProfilerMarker s_LateUpdateMarker = new("URTSensorManager.LateUpdate");
		private static readonly ProfilerMarker s_FindRenderersMarker = new("URTSensorManager.FindRenderers");
		private static readonly ProfilerMarker s_AddInstancesMarker = new("URTSensorManager.AddInstances");
		private static readonly ProfilerMarker s_BvhBuildMarker = new("URTSensorManager.BvhBuild");

		private RayTracingContext _rtContext;
		private IRayTracingAccelStruct _accelStruct;
		private GraphicsBuffer _buildScratchBuffer;
		private bool _isSupported;
		private MaterialPropertyBlock _tmpMpb;
		private CommandBuffer _cmd;

		// Robot model self-exclusion: each model root gets a bit index (0-7).
		private Dictionary<Transform, int> _modelBitIndices = new Dictionary<Transform, int>();
		private int _nextModelBit = 0;

		// ── BVH caching: skip full rebuild when scene hasn't changed ──
		private bool _bvhDirty = true;
		private bool _initialBuildDone = false;
		private Dictionary<Transform, Vector3> _cachedRobotPositions = new();
		private Dictionary<Transform, Quaternion> _cachedRobotRotations = new();
		private int _cachedRendererCount = -1;

		// ── Cached renderer lists: avoid FindObjectsByType every rebuild ──
		private MeshRenderer[] _cachedMeshRenderers;
		private SkinnedMeshRenderer[] _cachedSkinnedRenderers;
		private bool _renderersCached = false;

		// ── Timing diagnostics ──
		private readonly Stopwatch _rebuildSw = new();
		private float _diagAccumTime;
		private int _diagRebuildCount;
		private float _diagTotalRebuildMs;
		private float _diagMaxRebuildMs;
		private int _diagSkipCount;
		private const float DIAG_INTERVAL = 10f;

		public bool IsSupported => _isSupported;
		public RayTracingContext RTContext => _rtContext;
		public IRayTracingAccelStruct AccelStruct => _accelStruct;

		/// <summary>
		/// Force BVH rebuild next frame. Called when scene changes.
		/// </summary>
		public void SetDirty(bool invalidateRenderers = false)
		{
			_bvhDirty = true;
			if (invalidateRenderers) _renderersCached = false;
		}

		/// <summary>
		/// Create an IRayTracingShader from a shader Object (ComputeShader or RayTracingShader).
		/// Sensors load their .urtshader via Resources.Load and pass it here.
		/// </summary>
		public IRayTracingShader CreateShader(Object shader)
		{
			if (_rtContext == null || shader == null) return null;
			return _rtContext.CreateRayTracingShader(shader);
		}

		/// <summary>
		/// Get the ray inclusion mask a lidar should use to exclude its own model.
		/// </summary>
		public uint GetLidarInclusionMask(Transform sensorTransform)
		{
			var modelRoot = FindModelRoot(sensorTransform);
			if (modelRoot == null) return 0xFF;

			if (!_modelBitIndices.TryGetValue(modelRoot, out var bitIndex))
			{
				bitIndex = _nextModelBit++;
				_modelBitIndices[modelRoot] = bitIndex;
				if (bitIndex > 7)
					Debug.LogWarning("[URTSensorManager] More than 8 robot models — mask bits exhausted");
			}

			return 1u << bitIndex;
		}

		void Awake()
		{
			Instance = this;
			TryInitialize();
		}

		void Start()
		{
			// Retry in Start() in case the render pipeline wasn't ready during Awake()
			if (!_isSupported)
			{
				TryInitialize();
			}
		}

		private void TryInitialize()
		{
			if (_isSupported) return; // Already initialized

			// Check CLOISIM_FORCE_RASTER env var — when set to "1", skip RT and use rasterization
			var forceRaster = System.Environment.GetEnvironmentVariable("CLOISIM_FORCE_RASTER");
			if (forceRaster == "1")
			{
				Debug.Log("[URTSensorManager] CLOISIM_FORCE_RASTER=1 — forcing rasterization path");
				_isSupported = false;
				return;
			}

			// Load resources from render pipeline
			var resources = new RayTracingResources();
			bool resourcesLoaded = resources.LoadFromRenderPipelineResources();
			if (!resourcesLoaded)
			{
				Debug.LogWarning("[URTSensorManager] LoadFromRenderPipelineResources failed — trying reflection fallback");
				resourcesLoaded = TryLoadResourcesViaReflection(resources);
			}

			// If resources still not loaded but HW RT is supported, use empty resources.
			// The HardwareRayTracingBackend never accesses the compute shaders — they are
			// only needed by the ComputeRayTracingBackend (RadeonRays BVH software path).
			if (!resourcesLoaded)
			{
				if (RayTracingContext.IsBackendSupported(RayTracingBackend.Hardware))
				{
					Debug.Log("[URTSensorManager] Resource loading failed but HW RT available — using empty resources for Hardware backend");
					resources = new RayTracingResources();
				}
				else
				{
					Debug.LogWarning("[URTSensorManager] All resource loading methods failed and no HW RT — sensors will use rasterization");
					_isSupported = false;
					return;
				}
			}

			try
			{
				// Select backend: prefer hardware RT cores, fall back to compute (RadeonRays BVH)
				var backend = RayTracingContext.IsBackendSupported(RayTracingBackend.Hardware)
					? RayTracingBackend.Hardware
					: RayTracingBackend.Compute;
				_rtContext = new RayTracingContext(backend, resources);

				var options = new AccelerationStructureOptions
				{
					buildFlags = BuildFlags.PreferFastBuild
				};
				_accelStruct = _rtContext.CreateAccelerationStructure(options);

				_cmd = new CommandBuffer { name = "URTSensorManager" };
				_tmpMpb = new MaterialPropertyBlock();

				// Mark supported only after ALL objects are successfully created
				_isSupported = true;

				Debug.Log($"[URTSensorManager] Initialized — backend: {_rtContext.BackendType}");
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Failed to create RayTracingContext: {e.Message} — sensors will use rasterization");
				_isSupported = false;
			}
		}

		/// <summary>
		/// Fallback: use reflection to access the RayTracingRenderPipelineResources
		/// from the render pipeline global settings and copy the compute shaders into
		/// the RayTracingResources instance. This works when TryGetRenderPipelineSettings
		/// fails (e.g., the resource type is stripped from the player build).
		/// </summary>
		private bool TryLoadResourcesViaReflection(RayTracingResources resources)
		{
			try
			{
				// Get the current render pipeline global settings (public API)
				var rpAsset = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
				if (rpAsset == null)
				{
					Debug.LogWarning("[URTSensorManager] No render pipeline asset found");
					return false;
				}

				// Get the global settings object via reflection on GraphicsSettings
				// GraphicsSettings stores the global settings for the current render pipeline
				var gsType = typeof(GraphicsSettings);
				var currentSettingsProp = gsType.GetProperty("currentRenderPipelineGlobalSettings",
					System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

				object globalSettings = null;

				if (currentSettingsProp != null)
				{
					globalSettings = currentSettingsProp.GetValue(null);
				}

				if (globalSettings == null)
				{
					// Try the public GetSettingsForRenderPipeline method
					var getSettingsMethod = gsType.GetMethod("GetSettingsForRenderPipeline",
						System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public,
						null, new System.Type[] { typeof(RenderPipelineAsset) }, null);

					if (getSettingsMethod != null)
					{
						globalSettings = getSettingsMethod.Invoke(null, new object[] { rpAsset });
					}
				}

				if (globalSettings == null)
				{
					Debug.LogWarning("[URTSensorManager] Could not access render pipeline global settings");
					return false;
				}

				Debug.Log($"[URTSensorManager] Got global settings type: {globalSettings.GetType().Name}");

				// Search for RayTracingRenderPipelineResources in the settings list
				// RenderPipelineGlobalSettings stores IRenderPipelineGraphicsSettings in m_Settings
				var settingsObj = globalSettings;
				var settingsType = settingsObj.GetType();

				System.Reflection.FieldInfo settingsField = null;
				var searchType = settingsType;
				while (searchType != null && settingsField == null)
				{
					settingsField = searchType.GetField("m_Settings",
						System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					searchType = searchType.BaseType;
				}

				if (settingsField != null)
				{
					var settings = settingsField.GetValue(settingsObj) as System.Collections.IList;
					if (settings != null)
					{
						Debug.Log($"[URTSensorManager] Found settings list with {settings.Count} entries");
						foreach (var item in settings)
						{
							if (item == null) continue;
							var typeName = item.GetType().Name;
							Debug.Log($"[URTSensorManager]   Setting: {typeName}");
							if (typeName == "RayTracingRenderPipelineResources")
							{
								return CopyRtResourceFields(item, resources);
							}
						}
					}
				}

				Debug.LogWarning($"[URTSensorManager] RayTracingRenderPipelineResources not found in global settings");
				return false;
			}
			catch (System.Exception e)
			{
				Debug.LogWarning($"[URTSensorManager] Reflection fallback failed: {e.Message}\n{e.StackTrace}");
				return false;
			}
		}

		private bool CopyRtResourceFields(object rtPipelineResources, RayTracingResources target)
		{
			var srcType = rtPipelineResources.GetType();
			var targetType = typeof(RayTracingResources);

			string[] fieldNames = {
				"geometryPoolKernels", "copyBuffer", "copyPositions",
				"bitHistogram", "blockReducePart", "blockScan",
				"buildHlbvh", "restructureBvh", "scatter"
			};

			// In the source (RayTracingRenderPipelineResources), the fields are
			// m_GeometryPoolKernels etc. The target (RayTracingResources) has
			// internal properties: geometryPoolKernels etc.
			string[] srcFieldNames = {
				"m_GeometryPoolKernels", "m_CopyBuffer", "m_CopyPositions",
				"m_BitHistogram", "m_BlockReducePart", "m_BlockScan",
				"m_BuildHlbvh", "m_RestructureBvh", "m_Scatter"
			};

			int assigned = 0;
			for (int i = 0; i < fieldNames.Length; i++)
			{
				var srcField = srcType.GetField(srcFieldNames[i],
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (srcField == null) continue;

				var value = srcField.GetValue(rtPipelineResources) as ComputeShader;
				if (value == null)
				{
					Debug.LogWarning($"[URTSensorManager] Reflection: {srcFieldNames[i]} is null");
					continue;
				}

				var targetProp = targetType.GetProperty(fieldNames[i],
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
				if (targetProp != null && targetProp.CanWrite)
				{
					targetProp.SetValue(target, value);
					assigned++;
				}
			}

			Debug.Log($"[URTSensorManager] Reflection: assigned {assigned}/{fieldNames.Length} compute shaders");
			return assigned >= 6; // Need at least the core RadeonRays shaders
		}

		void LateUpdate()
		{
			if (!_isSupported || _accelStruct == null || _cmd == null) return;

			using (s_LateUpdateMarker.Auto())
			{
				// Check if robot models have moved
				if (!_bvhDirty && _initialBuildDone)
				{
					_bvhDirty = CheckRobotTransformsChanged();
				}

				if (_bvhDirty || !_initialBuildDone)
				{
					_rebuildSw.Restart();
					RebuildAccelStruct();
					_rebuildSw.Stop();

					_bvhDirty = false;
					_initialBuildDone = true;
					CacheRobotTransforms();

					var ms = (float)_rebuildSw.Elapsed.TotalMilliseconds;
					_diagRebuildCount++;
					_diagTotalRebuildMs += ms;
					if (ms > _diagMaxRebuildMs) _diagMaxRebuildMs = ms;
				}
				else
				{
					_diagSkipCount++;
				}

				// Periodic diagnostics
				_diagAccumTime += Time.unscaledDeltaTime;
				if (_diagAccumTime >= DIAG_INTERVAL)
				{
					var avgMs = _diagRebuildCount > 0 ? _diagTotalRebuildMs / _diagRebuildCount : 0;
					Debug.Log($"[URTSensorManager] {DIAG_INTERVAL}s: rebuilds={_diagRebuildCount}, skipped={_diagSkipCount}, " +
						$"avgRebuildMs={avgMs:F2}, maxRebuildMs={_diagMaxRebuildMs:F2}");
					_diagAccumTime = 0;
					_diagRebuildCount = 0;
					_diagTotalRebuildMs = 0;
					_diagMaxRebuildMs = 0;
					_diagSkipCount = 0;
				}
			}
		}

		/// <summary>
		/// Check if any robot model root transforms have moved significantly.
		/// Uses position + rotation threshold to avoid floating-point jitter.
		/// </summary>
		private bool CheckRobotTransformsChanged()
		{
			const float posThreshold = 0.0005f; // 0.5mm
			const float rotThreshold = 0.001f;  // ~0.06 degrees

			foreach (var kv in _modelBitIndices)
			{
				if (kv.Key == null) continue;

				// Check root transform only — if root hasn't moved, children are stable
				if (!_cachedRobotPositions.TryGetValue(kv.Key, out var cachedPos))
					return true;

				var currentPos = kv.Key.position;
				if ((currentPos - cachedPos).sqrMagnitude > posThreshold * posThreshold)
					return true;

				if (!_cachedRobotRotations.TryGetValue(kv.Key, out var cachedRot))
					return true;

				if (Quaternion.Angle(kv.Key.rotation, cachedRot) > rotThreshold)
					return true;

				// Also check direct children for articulated robots (arms, wheels)
				foreach (Transform child in kv.Key)
				{
					if (!_cachedRobotPositions.TryGetValue(child, out var childPos))
						return true;
					if ((child.localPosition - childPos).sqrMagnitude > posThreshold * posThreshold)
						return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Cache current robot root + child transforms for change detection.
		/// </summary>
		private void CacheRobotTransforms()
		{
			_cachedRobotPositions.Clear();
			_cachedRobotRotations.Clear();
			foreach (var kv in _modelBitIndices)
			{
				if (kv.Key == null) continue;
				_cachedRobotPositions[kv.Key] = kv.Key.position;
				_cachedRobotRotations[kv.Key] = kv.Key.rotation;
				foreach (Transform child in kv.Key)
				{
					_cachedRobotPositions[child] = child.localPosition;
				}
			}
		}

		private void RebuildAccelStruct()
		{
			_accelStruct.ClearInstances();

			// Discover model roots and assign bit indices
			DiscoverModelRoots();

			// Build renderer → mask mapping for robot bodies
			var robotMasks = new Dictionary<Renderer, uint>();
			foreach (var kv in _modelBitIndices)
			{
				if (kv.Key == null) continue;
				uint clearMask = 0xFFu ^ (1u << kv.Value);
				foreach (var r in kv.Key.GetComponentsInChildren<Renderer>(true))
				{
					robotMasks[r] = clearMask;
				}
			}

			int instanceCount = 0;
			int meshRendererCount = 0;
			int skinnedRendererCount = 0;

			// Use cached renderer lists after initial build (avoid expensive FindObjectsByType)
			MeshRenderer[] allRenderers;
			SkinnedMeshRenderer[] skinnedRenderers;
			using (s_FindRenderersMarker.Auto())
			{
				if (!_renderersCached)
				{
					_cachedMeshRenderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
					_cachedSkinnedRenderers = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
					_renderersCached = true;
				}
				allRenderers = _cachedMeshRenderers;
				skinnedRenderers = _cachedSkinnedRenderers;
				meshRendererCount = allRenderers.Length;
				skinnedRendererCount = skinnedRenderers.Length;
			}
			using (s_AddInstancesMarker.Auto())
			{
			foreach (var r in allRenderers)
			{
				if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
				if (r.gameObject.layer == 2) continue; // Skip Ignore Raycast

				var meshFilter = r.GetComponent<MeshFilter>();
				if (meshFilter == null || meshFilter.sharedMesh == null) continue;

				var mesh = meshFilter.sharedMesh;
				if (!mesh.isReadable) continue; // Unified RT compute backend requires readable meshes

				uint segId = GetSegmentationId(r);

				uint mask = robotMasks.ContainsKey(r) ? robotMasks[r] : 0xFFu;

				for (int subIdx = 0; subIdx < mesh.subMeshCount; subIdx++)
				{
					var desc = new MeshInstanceDesc(mesh, subIdx)
					{
						localToWorldMatrix = r.localToWorldMatrix,
						mask = mask,
						instanceID = segId,
						enableTriangleCulling = true,
						frontTriangleCounterClockwise = false,
						opaqueGeometry = true
					};

					try
					{
						_accelStruct.AddInstance(desc);
						instanceCount++;
					}
					catch (System.Exception)
					{
						// Mesh format not supported — skip silently
					}
				}
			}

			// Also add SkinnedMeshRenderers (robot bodies, animated objects)
			foreach (var smr in skinnedRenderers)
			{
				if (smr == null || !smr.enabled || !smr.gameObject.activeInHierarchy) continue;
				if (smr.gameObject.layer == 2) continue;

				var mesh = smr.sharedMesh;
				if (mesh == null || !mesh.isReadable) continue;

				uint segId = GetSegmentationId(smr);

				uint mask = robotMasks.ContainsKey(smr) ? robotMasks[smr] : 0xFFu;

				for (int subIdx = 0; subIdx < mesh.subMeshCount; subIdx++)
				{
					var desc = new MeshInstanceDesc(mesh, subIdx)
					{
						localToWorldMatrix = smr.localToWorldMatrix,
						mask = mask,
						instanceID = segId,
						enableTriangleCulling = true,
						frontTriangleCounterClockwise = false,
						opaqueGeometry = true
					};

					try
					{
						_accelStruct.AddInstance(desc);
						instanceCount++;
					}
					catch (System.Exception)
					{
						// Skip non-readable meshes
					}
				}
			}
			} // end using s_AddInstancesMarker

			_cachedRendererCount = instanceCount;

			// Build the acceleration structure
			using (s_BvhBuildMarker.Auto())
			{
				RayTracingHelper.ResizeScratchBufferForBuild(_accelStruct, ref _buildScratchBuffer);

				_cmd.Clear();
				_accelStruct.Build(_cmd, _buildScratchBuffer);
				Graphics.ExecuteCommandBuffer(_cmd);
			}

			if (!_initialBuildDone)
			{
				Debug.Log($"[URTSensorManager] Initial BVH build: {instanceCount} instances, " +
					$"{meshRendererCount} MR, {skinnedRendererCount} SMR");
			}
		}

		private void DiscoverModelRoots()
		{
			try
			{
				var modelObjects = GameObject.FindGameObjectsWithTag("Model");
				foreach (var go in modelObjects)
				{
					if (go == null) continue;
					if (!_modelBitIndices.ContainsKey(go.transform))
					{
						_modelBitIndices[go.transform] = _nextModelBit++;
						if (_nextModelBit > 8)
							Debug.LogWarning($"[URTSensorManager] Model bit overflow for '{go.name}'");
					}
				}
			}
			catch (UnityException)
			{
				// "Model" tag may not exist in this project — ignore
			}
		}

		private static Transform FindModelRoot(Transform current)
		{
			while (current != null)
			{
				if (current.CompareTag("Model"))
					return current;
				current = current.parent;
			}
			return null;
		}

		/// <summary>
		/// Get the segmentation class ID for a renderer by:
		///   1. Check for a Segmentation.Tag component on this or parent GameObjects
		///   2. Fall back to reading _SegmentationValue from per-material PropertyBlock
		/// SegmentationTag.AllocateMaterialPropertyBlock sets the property block per
		/// material index (renderer.SetPropertyBlock(mpb, i)), so the renderer-level
		/// GetPropertyBlock() without index returns an empty block.
		/// </summary>
		private uint GetSegmentationId(Renderer r)
		{
			// Primary: read from SegmentationTag component
			var tag = r.GetComponentInParent<Tag>();
			if (tag != null)
				return tag.ClassId;

			// Fallback: read per-material-index property block (index 0)
			if (_tmpMpb != null)
			{
				r.GetPropertyBlock(_tmpMpb, 0);
				var val = _tmpMpb.GetInt("_SegmentationValue");
				if (val != 0) return (uint)val;
			}

			return 0;
		}

		void OnDestroy()
		{
			_buildScratchBuffer?.Release();
			_accelStruct?.Dispose();
			_rtContext?.Dispose();
			_cmd?.Release();

			_buildScratchBuffer = null;
			_accelStruct = null;
			_rtContext = null;
			_cmd = null;
			Instance = null;
		}
	}
}
