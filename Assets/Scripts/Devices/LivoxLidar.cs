/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using messages = cloisim.msgs;

namespace SensorDevices
{
	/// <summary>
	/// Livox non-repetitive scan pattern LiDAR sensor device.
	/// Renders depth via URP sub-cameras (same approach as <see cref="Lidar"/>)
	/// but samples at non-uniform ray directions loaded from a CSV scan pattern file.
	/// Each frame advances through the pattern in a rolling window, producing the
	/// characteristic non-repetitive coverage of Livox sensors.
	/// Supported models: HAP, mid360, avia, horizon, mid40, mid70, tele.
	/// </summary>
	public class LivoxLidar : Device
	{
		// ── Profiling ──
		private static readonly ProfilerMarker s_SubCamRenderMarker = new("LivoxLidar.SubCamRender");
		private static readonly ProfilerMarker s_ComputeMarker = new("LivoxLidar.ComputeDispatch");

		private static int _globalSequence = 0;

		// ── Message ──
		[SerializeField] private messages.LaserScan _laserScan = null;
		[SerializeField] private Thread _processThread = null;
		public Action<messages.LaserScanStamped> OnLidarDataGenerated;

		// ── Constants ──
		private const float DEG360 = 360f;
		private const float SUB_CAM_HFOV = 60f;
		private const float SUB_CAM_HFOV_HALF = SUB_CAM_HFOV * 0.5f;
		/// <summary>Render target angular resolution in degrees. Determines pixel density.</summary>
		private const float RT_ANGULAR_RES = 0.2f;

		// ── Livox parameters (set from SDF) ──
		[Header("Livox Parameters")]
		[SerializeField] private string _scanMode = "mid360";
		[SerializeField] private int _samplesPerFrame = 24000;
		[SerializeField] private int _downSample = 1;
		private LivoxScanPattern _scanPattern;

		// ── Shared lidar parameters ──
		[Header("SDF Properties")]
		private MathUtil.MinMax _scanRange;
		private float _rangeResolution = 0;
		private Noise _noise = null;
		private string _noiseParamInRawXml;

		// ── Public setters (called by Implement.Sensor.cs / Plugin) ──
		public string ScanMode { get => _scanMode; set => _scanMode = value; }
		public int SamplesPerFrame { get => _samplesPerFrame; set => _samplesPerFrame = value; }
		public int DownSample { get => _downSample; set => _downSample = value; }
		public MathUtil.MinMax ScanRange { get => _scanRange; set => _scanRange = value; }
		public float RangeResolution { get => _rangeResolution; set => _rangeResolution = value; }

		/// <summary>
		/// Output format: "LaserScan" (default, flat 2D) or "PointCloud2Raw" (pre-computed xyz).
		/// Set by the plugin based on SDF &lt;output_type&gt; parameter.
		/// </summary>
		public string OutputType { get; set; } = "LaserScan";

		// ── Camera ──
		private UnityEngine.Camera _laserCam;
		private float _cameraVFov;
		private float _cameraVFovHalf;
		private int _numSubCameras;

		// ── Self-occlusion ──
		private Renderer[] _parentModelRenderers;
		private int[] _parentModelOriginalLayers;
		private const int SelfOcclusionLayer = 2;

		// ── Sub-camera layout ──
		private struct SubCamInfo
		{
			public float rotationAngle; // Y-rotation in degrees
			public bool isActive;       // whether any scan rays could fall in this camera
		}
		private SubCamInfo[] _subCamInfo;

		// ── GPU resources ──
		private RTHandle _rtHandle;
		private Material _depthCaptureMaterial;
		private RenderTexture _capturedDepthRT;
		private ComputeShader _livoxCompute;
		private int _computeKernel;

		// ── Compute buffers (ring) ──
		private const int BufferCount = 5;
		private ComputeBuffer[] _outputBuffers;
		private ComputeBuffer _rayBuffer;
		private int _bufferIndex;
		private float[] _nanInitData; // pre-filled NaN array for clearing output buffer

		// ── Ray assignment workspace ──
		[StructLayout(LayoutKind.Sequential)]
		private struct GpuLivoxRay
		{
			public float hAngleLocalRad;
			public float vAngleRad;
			public uint outputIndex;
		}
		private GpuLivoxRay[] _gpuRayWorkspace;
		private LivoxRayInfo[] _frameRayWindow;
		private int[] _bestCamForRay;
		private int _actualSamplesPerFrame;

		// ── Processing ──
		private bool _running;
		private ConcurrentQueue<(double time, Pose pose, float[] ranges, float[] azRad, float[] elRad)> _outputQueue = new();
		private readonly AutoResetEvent _dataAvailable = new(false);

		// ═══════════════════════════════════════════════
		//  URP DEPTH CAPTURE (same pattern as Lidar.cs)
		// ═══════════════════════════════════════════════

		private void OnEnable()
		{
			if (GraphicsSettings.currentRenderPipeline != null)
				RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
		}

		private void OnDisable()
		{
			if (GraphicsSettings.currentRenderPipeline != null)
				RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
		}

		private void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera != _laserCam || _depthCaptureMaterial == null)
				return;

			var w = camera.pixelWidth;
			var h = camera.pixelHeight;

			if (_capturedDepthRT == null || _capturedDepthRT.width != w || _capturedDepthRT.height != h)
			{
				if (_capturedDepthRT != null) _capturedDepthRT.Release();
				_capturedDepthRT = new RenderTexture(w, h, 0, GraphicsFormat.R32_SFloat)
				{
					name = "LivoxCapturedDepth",
					filterMode = FilterMode.Point,
				};
				_capturedDepthRT.Create();
			}

			var cmd = CommandBufferPool.Get("LivoxDepthCapture");
			CoreUtils.SetRenderTarget(cmd, _capturedDepthRT);
			CoreUtils.DrawFullScreen(cmd, _depthCaptureMaterial, shaderPassId: 0);
			context.ExecuteCommandBuffer(cmd);
			context.Submit();
			CommandBufferPool.Release(cmd);
		}

		// ═══════════════════════════════════════════════
		//  LIFECYCLE
		// ═══════════════════════════════════════════════

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;

			_livoxCompute = Instantiate(Resources.Load<ComputeShader>("Shader/LivoxLaserCamData"));

			var laserSensor = new GameObject("__livox_laser__");
			laserSensor.transform.SetParent(transform, false);
			laserSensor.transform.localPosition = Vector3.zero;
			laserSensor.transform.localRotation = Quaternion.identity;
			_laserCam = laserSensor.AddComponent<UnityEngine.Camera>();
			_laserCam.enabled = false;

			_processThread = new Thread(ProcessingLoop);
		}

		protected override void OnStart()
		{
			if (_laserCam == null) return;

			// _scanPattern and _actualSamplesPerFrame already initialized in SetupMessages()
			if (_scanPattern == null)
			{
				Debug.LogError($"[LivoxLidar] Scan pattern not loaded: {_scanMode}");
				return;
			}

			// Allocate workspace arrays
			_frameRayWindow = new LivoxRayInfo[_actualSamplesPerFrame];
			_gpuRayWorkspace = new GpuLivoxRay[_actualSamplesPerFrame];
			_bestCamForRay = new int[_actualSamplesPerFrame];

			// NaN init data for clearing GPU output buffer each frame
			_nanInitData = new float[_actualSamplesPerFrame];
			Array.Fill(_nanInitData, float.NaN);

			SetupCamera();
			SetupSubCameras();
			SetupComputeResources();

			_running = true;
			Invoke(nameof(StartCaptureDelayed), 0.1f);

			_processThread?.Start();

			// Self-occlusion: cache parent model renderers
			_parentModelRenderers = FindParentModelRenderers();
			if (_parentModelRenderers != null)
			{
				_parentModelOriginalLayers = new int[_parentModelRenderers.Length];
				var modelName = "unknown";
				var current = transform.parent;
				while (current != null)
				{
					if (current.CompareTag("Model")) { modelName = current.name; break; }
					current = current.parent;
				}
				Debug.Log($"[LivoxLidar] Found {_parentModelRenderers.Length} renderers in " +
						  $"model '{modelName}' for self-occlusion (layer={SelfOcclusionLayer})");
			}
		}

		// ═══════════════════════════════════════════════
		//  CAMERA SETUP
		// ═══════════════════════════════════════════════

		private void SetupCamera()
		{
			// Compute vertical FOV from scan pattern's elevation bounds.
			// The camera VFOV must be large enough so that all rays project
			// within the render target even at the sub-camera horizontal edges.
			// At horizontal offset h from camera center, a ray at elevation e
			// projects to ndc.y = tan(e) / (cos(h) * tan(VFOV/2)).
			// To keep ndc.y ≤ 1 at h = SUB_CAM_HFOV/2, we need:
			//   VFOV/2 ≥ atan(tan(maxElev) / cos(SUB_CAM_HFOV/2))
			var maxElevAbs = Mathf.Max(
				Mathf.Abs(_scanPattern.ElevationMinDeg),
				Mathf.Abs(_scanPattern.ElevationMaxDeg));
			var cosHalfHFov = Mathf.Cos(SUB_CAM_HFOV_HALF * Mathf.Deg2Rad);
			var expandedHalfVFov = Mathf.Atan(Mathf.Tan(maxElevAbs * Mathf.Deg2Rad) / cosHalfHFov) * Mathf.Rad2Deg;
			_cameraVFov = Mathf.Max(expandedHalfVFov * 2f, 1f);
			_cameraVFovHalf = _cameraVFov * 0.5f;

			_laserCam.ResetWorldToCameraMatrix();
			_laserCam.ResetProjectionMatrix();
			_laserCam.allowHDR = false;
			_laserCam.allowMSAA = false;
			_laserCam.allowDynamicResolution = false;
			_laserCam.useOcclusionCulling = false;
			_laserCam.usePhysicalProperties = false;
			_laserCam.orthographic = false;
			_laserCam.nearClipPlane = _scanRange.min;
			_laserCam.farClipPlane = _scanRange.max;
			_laserCam.cullingMask = LayerMask.GetMask("Default", "Plane");
			_laserCam.clearFlags = CameraClearFlags.Depth;
			_laserCam.depthTextureMode = DepthTextureMode.Depth;

			var rtWidth = Mathf.CeilToInt(SUB_CAM_HFOV / RT_ANGULAR_RES);
			var rtHeight = Mathf.CeilToInt(_cameraVFov / RT_ANGULAR_RES);

			RTHandles.SetHardwareDynamicResolutionState(false);
			_rtHandle?.Release();
			_rtHandle = RTHandles.Alloc(
				width: rtWidth, height: rtHeight, slices: 1,
				depthBufferBits: DepthBits.None,
				colorFormat: GraphicsFormat.R32_SFloat,
				filterMode: FilterMode.Point,
				wrapMode: TextureWrapMode.Clamp,
				dimension: TextureDimension.Tex2D,
				msaaSamples: MSAASamples.None,
				enableRandomWrite: false,
				useMipMap: false,
				autoGenerateMips: false,
				isShadowMap: false,
				anisoLevel: 0,
				mipMapBias: 0,
				bindTextureMS: false,
				useDynamicScale: false,
				memoryless: RenderTextureMemoryless.None,
				name: "RT_LivoxDepth");

			_laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = SensorHelper.MakeProjectionMatrixPerspective(
				SUB_CAM_HFOV, _cameraVFov, _laserCam.nearClipPlane, _laserCam.farClipPlane);
			_laserCam.projectionMatrix = projMatrix;

			// URP camera optimizations — disable all expensive features
			// (same pattern as Lidar.cs for consistency)
			var universalLaserCamData = _laserCam.GetUniversalAdditionalCameraData();
			universalLaserCamData.renderShadows = false;
			universalLaserCamData.stopNaN = true;
			universalLaserCamData.dithering = false;
			universalLaserCamData.allowXRRendering = false;
			universalLaserCamData.volumeLayerMask = default;
			universalLaserCamData.renderType = CameraRenderType.Base;
			universalLaserCamData.renderPostProcessing = false;
			universalLaserCamData.antialiasing = AntialiasingMode.None;
			universalLaserCamData.requiresColorOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresDepthOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresColorTexture = false;
			universalLaserCamData.requiresDepthTexture = true;
			universalLaserCamData.cameraStack.Clear();

			// URP depth-capture material — uses the same DepthRange shader as Lidar
			var depthShader = Shader.Find("Sensor/DepthRange");
			if (depthShader != null)
			{
				_depthCaptureMaterial = new Material(depthShader);
				_depthCaptureMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
				// Disable the horizontal flip: the depth buffer from Camera.Render()
				// already has the correct left-right orientation.
				_depthCaptureMaterial.SetInt("_FlipX", 0);
			}

			// Noise
			if (_noise != null)
			{
				_noise.SetCustomNoiseParameter(_noiseParamInRawXml);
				_noise.SetClampMin(_scanRange.min);
				_noise.SetClampMax(_scanRange.max);
			}

			Debug.Log($"[LivoxLidar] Camera: HFOV={SUB_CAM_HFOV}° VFOV={_cameraVFov:F1}° " +
					  $"RT={rtWidth}x{rtHeight} range=[{_scanRange.min}, {_scanRange.max}]");
		}

		private void SetupSubCameras()
		{
			_numSubCameras = Mathf.CeilToInt(DEG360 / SUB_CAM_HFOV);
			_subCamInfo = new SubCamInfo[_numSubCameras];

			var isEven = _numSubCameras % 2 == 0;
			var centerOffset = isEven ? -SUB_CAM_HFOV_HALF : 0f;

			float scanCenter, scanHalfFov;
			if (_scanPattern.IsFullRotation)
			{
				scanCenter = 180f;
				scanHalfFov = 180f;
			}
			else
			{
				scanCenter = (_scanPattern.AzimuthMinDeg + _scanPattern.AzimuthMaxDeg) * 0.5f;
				scanHalfFov = (_scanPattern.AzimuthMaxDeg - _scanPattern.AzimuthMinDeg) * 0.5f;
			}

			var activeCams = 0;
			for (var i = 0; i < _numSubCameras; i++)
			{
				var centerAngle = SUB_CAM_HFOV * i + centerOffset;
				_subCamInfo[i].rotationAngle = centerAngle;

				if (_scanPattern.IsFullRotation)
				{
					_subCamInfo[i].isActive = true;
				}
				else
				{
					var angleDiff = Mathf.Abs(Mathf.DeltaAngle(scanCenter, centerAngle));
					_subCamInfo[i].isActive = angleDiff <= (scanHalfFov + SUB_CAM_HFOV_HALF);
				}

				if (_subCamInfo[i].isActive) activeCams++;
			}

			Debug.Log($"[LivoxLidar] {_numSubCameras} sub-cameras, {activeCams} active " +
					  $"(scanCenter={scanCenter:F1}° halfFov={scanHalfFov:F1}°)");
		}

		private void SetupComputeResources()
		{
			_computeKernel = _livoxCompute.FindKernel("ComputeLivoxData");

			var targetTex = _laserCam.targetTexture;
			_livoxCompute.SetInt("_Width", targetTex.width);
			_livoxCompute.SetInt("_Height", targetTex.height);
			_livoxCompute.SetFloat("_MaxHAngleHalf", SUB_CAM_HFOV_HALF);
			_livoxCompute.SetFloat("_MaxVAngleHalf", _cameraVFovHalf);
			_livoxCompute.SetFloat("_MaxHAngleHalfTanInv",
				1f / Mathf.Tan(SUB_CAM_HFOV_HALF * Mathf.Deg2Rad));
			_livoxCompute.SetFloat("_MaxVAngleHalfTanInv",
				1f / Mathf.Tan(_cameraVFovHalf * Mathf.Deg2Rad));
			_livoxCompute.SetFloat("_RangeMin", _scanRange.min);
			_livoxCompute.SetFloat("_RangeMax", _scanRange.max);

			// Output buffers (ring of BufferCount)
			_outputBuffers = new ComputeBuffer[BufferCount];
			for (var b = 0; b < BufferCount; b++)
				_outputBuffers[b] = new ComputeBuffer(_actualSamplesPerFrame, sizeof(float));

			// Ray angle buffer (reused each dispatch)
			// Stride = 3 floats: hAngleLocalRad, vAngleRad, outputIndex(uint=4bytes)
			_rayBuffer = new ComputeBuffer(_actualSamplesPerFrame, Marshal.SizeOf<GpuLivoxRay>());

			Debug.Log($"[LivoxLidar] Compute: kernel={_computeKernel} " +
					  $"samplesPerFrame={_actualSamplesPerFrame} buffers={BufferCount}");
		}

		// ═══════════════════════════════════════════════
		//  CAPTURE COROUTINE
		// ═══════════════════════════════════════════════

		private void StartCaptureDelayed()
		{
			if (_running)
				StartCoroutine(CaptureCoroutine());
		}

		private IEnumerator WaitStartSequence()
		{
			var seq = _globalSequence++;
			for (var i = 0; i < seq; i++)
				yield return null;
		}

		/// <summary>
		/// Main capture loop. Each frame:
		/// 1. Get a window of ray directions from the scan pattern.
		/// 2. Pre-assign each ray to the best sub-camera.
		/// 3. For each active sub-camera: render, filter rays, dispatch compute shader.
		/// 4. Async GPU readback → processing thread → message queue.
		/// </summary>
		private IEnumerator CaptureCoroutine()
		{
			yield return WaitStartSequence();

			var axisRotation = Vector3.zero;
			var lastUpdateTime = 0f;

			while (_running)
			{
				lastUpdateTime += Time.unscaledDeltaTime;
				if (lastUpdateTime < UpdatePeriod)
				{
					yield return null;
					continue;
				}
				lastUpdateTime -= UpdatePeriod;

				var capturedTime = GetNextSyntheticTime();
				var sensorPose = new Pose(transform.position, transform.rotation);
				_bufferIndex = (_bufferIndex + 1) % BufferCount;
				var currentOutputBuffer = _outputBuffers[_bufferIndex];

				// Clear output buffer with NaN (unsampled rays remain NaN)
				currentOutputBuffer.SetData(_nanInitData);

				// Get this frame's ray window from the rolling scan pattern
				var rayCount = _scanPattern.GetRayWindow(
					_samplesPerFrame, _downSample, _frameRayWindow);

				// Pre-assign each ray to the best (closest center) active sub-camera.
				// This ensures each ray is processed exactly once, even in overlap regions.
				for (var r = 0; r < rayCount; r++)
					_bestCamForRay[r] = GetBestSubCamera(_frameRayWindow[r].azimuthDeg);

				// Hide robot's own body
				HideParentModel();

				// Render each active sub-camera and dispatch compute for its rays
				for (var camIdx = 0; camIdx < _numSubCameras; camIdx++)
				{
					if (!_subCamInfo[camIdx].isActive)
						continue;

					var camCenterDeg = _subCamInfo[camIdx].rotationAngle;

					// Rotate camera to this sub-camera's direction
					axisRotation.y = camCenterDeg;
					_laserCam.transform.localRotation = Quaternion.Euler(axisRotation);

					using (s_SubCamRenderMarker.Auto())
					{
						_laserCam.Render();
					}

					// Collect rays assigned to this camera
					var matchCount = 0;
					for (var r = 0; r < rayCount; r++)
					{
						if (_bestCamForRay[r] != camIdx)
							continue;

						var ray = _frameRayWindow[r];
						var deltaAngle = Mathf.DeltaAngle(camCenterDeg, ray.azimuthDeg);

						_gpuRayWorkspace[matchCount] = new GpuLivoxRay
						{
							hAngleLocalRad = deltaAngle * Mathf.Deg2Rad,
							vAngleRad = ray.elevationDeg * Mathf.Deg2Rad,
							outputIndex = (uint)r
						};
						matchCount++;
					}

					if (matchCount == 0)
						continue;

					// Upload ray data and dispatch compute shader
					using (s_ComputeMarker.Auto())
					{
						_rayBuffer.SetData(_gpuRayWorkspace, 0, 0, matchCount);
						_livoxCompute.SetInt("_RayCount", matchCount);

						var depthSource = _capturedDepthRT != null
							? (Texture)_capturedDepthRT
							: (Texture)_laserCam.targetTexture;
						_livoxCompute.SetTexture(_computeKernel, "_DepthTexture", depthSource);
						_livoxCompute.SetBuffer(_computeKernel, "_Rays", _rayBuffer);
						_livoxCompute.SetBuffer(_computeKernel, "_RayData", currentOutputBuffer);

						var groups = Mathf.CeilToInt(matchCount / 64f);
						_livoxCompute.Dispatch(_computeKernel, groups, 1, 1);
					}

					_laserCam.enabled = false;
				}

				// Restore robot visibility
				RestoreParentModel();

				// Capture per-ray angles for this frame (needed for PointCloud2 xyz)
				// Must copy before async readback since _frameRayWindow is shared.
				var frameRayCount = rayCount;
				var framePose = sensorPose;
				var frameTime = capturedTime;
				var frameAz = new float[rayCount];
				var frameEl = new float[rayCount];
				for (var i = 0; i < rayCount; i++)
				{
					frameAz[i] = _frameRayWindow[i].azimuthDeg * Mathf.Deg2Rad;
					frameEl[i] = _frameRayWindow[i].elevationDeg * Mathf.Deg2Rad;
				}

				// Async GPU readback
				AsyncGPUReadback.Request(currentOutputBuffer, (req) =>
				{
					if (req.hasError || !req.done)
					{
						Debug.LogWarning("[LivoxLidar] GPU readback error");
						return;
					}

					var src = req.GetData<float>();
					var ranges = new float[frameRayCount];
					for (var i = 0; i < frameRayCount; i++)
						ranges[i] = src[i];

					_outputQueue.Enqueue((frameTime, framePose, ranges, frameAz, frameEl));
					_dataAvailable.Set();
				});

				yield return null;
			}
		}

		/// <summary>
		/// Find the active sub-camera whose center is closest to the given azimuth.
		/// </summary>
		private int GetBestSubCamera(float azimuthDeg)
		{
			var bestIdx = 0;
			var bestDist = float.MaxValue;
			for (var i = 0; i < _numSubCameras; i++)
			{
				if (!_subCamInfo[i].isActive) continue;
				var dist = Mathf.Abs(Mathf.DeltaAngle(_subCamInfo[i].rotationAngle, azimuthDeg));
				if (dist < bestDist)
				{
					bestDist = dist;
					bestIdx = i;
				}
			}
			return bestIdx;
		}

		// ═══════════════════════════════════════════════
		//  MESSAGES
		// ═══════════════════════════════════════════════

		protected override void InitializeMessages()
		{
			_laserScan = new messages.LaserScan();
			_laserScan.WorldPose = new messages.Pose();
			_laserScan.WorldPose.Position = new messages.Vector3d();
			_laserScan.WorldPose.Orientation = new messages.Quaternion();
		}

		protected override void SetupMessages()
		{
			// Load scan pattern here because SetupMessages() runs before OnStart()
			_scanPattern = LivoxScanPattern.Load(_scanMode);
			if (_scanPattern == null)
			{
				Debug.LogError($"[LivoxLidar] Failed to load scan pattern for SetupMessages: {_scanMode}");
				return;
			}

			// Compute actual samples per frame after downsampling
			_actualSamplesPerFrame = Mathf.CeilToInt((float)_samplesPerFrame / Mathf.Max(1, _downSample));

			_laserScan.Frame = DeviceName;
			_laserScan.Count = (uint)_actualSamplesPerFrame;

			// For non-uniform scan patterns, angle fields represent the bounding range.
			// Individual ray angles vary and are defined by the CSV scan pattern.
			var azMinRad = _scanPattern.AzimuthMinDeg * Mathf.Deg2Rad;
			var azMaxRad = _scanPattern.AzimuthMaxDeg * Mathf.Deg2Rad;
			_laserScan.AngleMin = azMinRad;
			_laserScan.AngleMax = azMaxRad;
			_laserScan.AngleStep = (azMaxRad - azMinRad) / Mathf.Max(1, _actualSamplesPerFrame);

			_laserScan.RangeMin = _scanRange.min;
			_laserScan.RangeMax = _scanRange.max;

			_laserScan.VerticalCount = 1;
			_laserScan.VerticalAngleMin = _scanPattern.ElevationMinDeg * Mathf.Deg2Rad;
			_laserScan.VerticalAngleMax = _scanPattern.ElevationMaxDeg * Mathf.Deg2Rad;
			_laserScan.VerticalAngleStep = 0;

			// For PointCloud2Raw, ranges stores xyz triples (3× size).
			// OutputType may not be set yet at this point (set by plugin),
			// so we allocate the larger size and trim in ProcessingLoop if needed.
			_laserScan.Ranges = new double[_actualSamplesPerFrame * 3];
			_laserScan.Intensities = new double[_actualSamplesPerFrame];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, 0.0);
		}

		// ═══════════════════════════════════════════════
		//  PROCESSING THREAD
		// ═══════════════════════════════════════════════

		private void ProcessingLoop()
		{
			while (_running)
			{
				if (_outputQueue.TryDequeue(out var item))
				{
					var isRawPC2 = OutputType == "PointCloud2Raw";
					var (capturedTime, sensorPose, ranges, azRad, elRad) = item;

					var laserScanStamped = new messages.LaserScanStamped();
					laserScanStamped.Time = new messages.Time();
					laserScanStamped.Time.Set(capturedTime);
					laserScanStamped.Scan = _laserScan;

					var scan = laserScanStamped.Scan;
					scan.WorldPose.Position.Set(sensorPose.position);
					scan.WorldPose.Orientation.Set(sensorPose.rotation);

					if (isRawPC2)
					{
						// PointCloud2Raw: pack pre-computed xyz into Ranges
						// Ranges layout: [x0, y0, z0, x1, y1, z1, ...]
						Array.Fill(scan.Ranges, double.NaN);
						for (var i = 0; i < ranges.Length && i < _actualSamplesPerFrame; i++)
						{
							var r = (double)ranges[i];
							if (double.IsNaN(r) || double.IsInfinity(r))
								continue;

							// Apply noise to range before xyz conversion
							if (_noise != null)
							{
								var tmp = new double[] { r };
								_noise.Apply<double>(tmp);
								r = tmp[0];
							}

							var cosEl = Math.Cos(elRad[i]);
							scan.Ranges[i * 3]     = r * cosEl * Math.Cos(azRad[i]);
							scan.Ranges[i * 3 + 1] = r * cosEl * Math.Sin(azRad[i]);
							scan.Ranges[i * 3 + 2] = r * Math.Sin(elRad[i]);
						}
					}
					else
					{
						// LaserScan mode: flat ranges
						Array.Fill(scan.Ranges, double.NaN);
						for (var i = 0; i < ranges.Length && i < scan.Ranges.Length; i++)
							scan.Ranges[i] = ranges[i];

						if (_noise != null)
							_noise.Apply<double>(scan.Ranges);
					}

					OnLidarDataGenerated?.Invoke(laserScanStamped);
					_messageQueue.Enqueue(laserScanStamped);
				}
				else
				{
					_dataAvailable.WaitOne();
				}
			}
		}

		// ═══════════════════════════════════════════════
		//  SELF-OCCLUSION
		// ═══════════════════════════════════════════════

		private Renderer[] FindParentModelRenderers()
		{
			var current = transform.parent;
			while (current != null)
			{
				if (current.CompareTag("Model"))
				{
					var renderers = current.GetComponentsInChildren<Renderer>(true);
					return renderers.Length > 0 ? renderers : null;
				}
				current = current.parent;
			}
			return null;
		}

		private void HideParentModel()
		{
			if (_parentModelRenderers == null) return;
			for (var i = 0; i < _parentModelRenderers.Length; i++)
			{
				if (_parentModelRenderers[i] != null)
				{
					_parentModelOriginalLayers[i] = _parentModelRenderers[i].gameObject.layer;
					_parentModelRenderers[i].gameObject.layer = SelfOcclusionLayer;
				}
			}
		}

		private void RestoreParentModel()
		{
			if (_parentModelRenderers == null) return;
			for (var i = 0; i < _parentModelRenderers.Length; i++)
			{
				if (_parentModelRenderers[i] != null)
					_parentModelRenderers[i].gameObject.layer = _parentModelOriginalLayers[i];
			}
		}

		// ═══════════════════════════════════════════════
		//  NOISE
		// ═══════════════════════════════════════════════

		public void SetupNoise(in SDF.Noise param)
		{
			if (param != null)
			{
				Debug.Log($"{DeviceName}: Apply noise type:{param.type} mean:{param.mean} stddev:{param.stddev}");
				_noise = new Noise(param);
			}
		}

		public void SetupCustomNoise(in string noiseParamInRawXml)
		{
			_noiseParamInRawXml = noiseParamInRawXml;
		}

		// ═══════════════════════════════════════════════
		//  CLEANUP
		// ═══════════════════════════════════════════════

		protected new void OnDestroy()
		{
			_running = false;
			_dataAvailable.Set();
			_outputQueue.Clear();

			if (_processThread != null && _processThread.IsAlive)
				_processThread.Join();

			StopAllCoroutines();

			if (_outputBuffers != null)
			{
				foreach (var buf in _outputBuffers) buf?.Release();
				_outputBuffers = null;
			}
			_rayBuffer?.Release();
			_rayBuffer = null;

			if (_depthCaptureMaterial != null)
				Destroy(_depthCaptureMaterial);

			if (_capturedDepthRT != null)
				_capturedDepthRT.Release();

			_rtHandle?.Release();
			if (_livoxCompute != null) Destroy(_livoxCompute);

			base.OnDestroy();
		}

		// ═══════════════════════════════════════════════
		//  PUBLIC API
		// ═══════════════════════════════════════════════

		public IReadOnlyList<double> GetRangeData()
		{
			try { return Array.AsReadOnly(_laserScan.Ranges); }
			catch { return null; }
		}
	}
}
