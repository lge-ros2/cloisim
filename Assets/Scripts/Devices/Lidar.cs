/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device, ISensorRenderable
	{
		// ── Profiling markers ──
		private static readonly ProfilerMarker s_LidarSubCamRenderMarker = new("Lidar.SubCamRender");
		private static readonly ProfilerMarker s_LidarComputeMarker = new("Lidar.ComputeDispatch");
		private static readonly ProfilerMarker s_LidarProcessMarker = new("Lidar.LaserProcessing");

		private static int _globalSequence = 0;
		[SerializeField] private messages.LaserScan _laserScan = null;
		[SerializeField] private Thread _laserProcessThread = null;
		public Action<messages.LaserScanStamped> OnLidarDataGenerated;

		[SerializeField] private const float DEG180 = Mathf.PI * Mathf.Rad2Deg;
		[SerializeField] private const float DEG360 = DEG180 * 2;

		[SerializeField] private const float HFOV_FOR_2D_LIDAR = 90f;
		// Wider FOV = fewer sub-cameras = fewer GPU render passes per scan.
		// 10deg -> 36 sub-cameras, 60deg -> 6 sub-cameras (6x fewer render calls).
		// The GPU processes tiny render targets (25px wide at 10deg) instantly,
		// so the bottleneck is CPU-side render setup per render pass.
		// At 60deg, render targets are 150px wide -- still trivial for GPU.
		[SerializeField] private const float HFOV_FOR_3D_LIDAR = 90f;
		[SerializeField] private float LaserCameraHFov = 0f;
		[SerializeField] private float LaserCameraHFovHalf = 0;
		[SerializeField] private float LaserCameraVFov = 0;
		[SerializeField] private float LaserCameraVFovOriginal = 0;

		[Header("SDF properties")]
		private MathUtil.MinMax _scanRange;
		private LaserData.Resolution _resolution;
		private LaserData.Scan _horizontal;
		private LaserData.Scan _vertical;
		private string _noiseParamInRawXml;
		private LaserFilter _laserFilter = null;
		private Noise _noise = null;

		public MathUtil.MinMax ScanRange
		{
			get => _scanRange;
			set => _scanRange = value;
		}

		public LaserData.Resolution Resolution
		{
			get => _resolution;
			set => _resolution = value;
		}

		public LaserData.Scan Horizontal
		{
			get => _horizontal;
			set => _horizontal = value;
		}

		public LaserData.Scan Vertical
		{
			get => _vertical;
			set => _vertical = value;
		}

		public string NoiseParamInRawXml
		{
			get => _noiseParamInRawXml;
			set => _noiseParamInRawXml = value;
		}

		[Header("Processing")]
		private Transform _lidarLink = null;
		private UnityEngine.Camera _laserCam = null;

		/// <summary>
		/// Cached renderers of the parent robot model, used to hide the robot's
		/// own body during LiDAR rendering to prevent self-occlusion.
		/// </summary>
		private Renderer[] _parentModelRenderers = null;
		private int[] _parentModelOriginalLayers = null;
		// Unity built-in layer 2 = "Ignore Raycast" -- always exists, not in the lidar culling mask
		private const int SelfOcclusionLayer = 2;

		// ── Unified Ray Tracing path ──
		// When Unified RT is available (hardware or compute backend), replaces
		// the entire sub-camera rasterization pipeline with a single dispatch.
		// Eliminates: N Camera.Render() calls, render graph setup per camera,
		// depth blit, LaserProcessing thread stitching.
		private bool _useURT = false;
		private UnityEngine.Rendering.UnifiedRayTracing.IRayTracingShader _urtShader;
		private GraphicsBuffer _urtOutputBuffer;
		private GraphicsBuffer _urtScratchBuffer;
		private uint _urtInclusionMask = 0xFF;
		private CommandBuffer _urtCmd;

		private int _numberOfLaserCamData = 0;
		private LaserData.CameraControlInfo[] _camControlInfo;

		private bool _startLaserWork = false;

		private RTHandle _rtHandle = null;
		private ParallelOptions _parallelOptions = null;

		private Material _depthMaterial;
		private CommandBuffer _cb;
		private ComputeShader _laserCompute;

		private int _laserComputeGroupsX;
		private int _laserComputeGroupsY;
		private int _laserComputeKernel;

		private int _horizontalBufferLength;
		private int _outputBufferLength;
		private ConcurrentQueue<(double, Pose, LaserData.Output[])> _outputQueue = new();
		private readonly AutoResetEvent _dataAvailable = new(false);

		// ── Render state for SensorRenderManager integration ──
		private const int BufferCount = 5;
		private ComputeBuffer[] _computeBuffers;
		private int _bufferIndex = 0;

		// ── ISensorRenderable: phase-locked scheduling ──
		private float _nextRenderTime = -1f;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_lidarLink = transform.parent;

			_laserCompute = Instantiate(Resources.Load<ComputeShader>("Shader/LaserCamData"));

			var laserSensor = new GameObject("__laser__");
			laserSensor.transform.SetParent(this.transform, false);
			laserSensor.transform.localPosition = Vector3.zero;
			laserSensor.transform.localRotation = Quaternion.identity;
			_laserCam = laserSensor.AddComponent<UnityEngine.Camera>();
			_laserCam.enabled = false;

			_laserProcessThread = new Thread(() => LaserProcessing());
		}

		protected override void OnStart()
		{
			if (_laserCam != null)
			{
				SetupLaserCamera();

				SetupLaserCameraData();

				_startLaserWork = true;

				// Try to initialize URT ray tracing path
				InitURT();

				if (!_useURT)
				{
					// Pre-allocate compute buffer ring for rasterization path
					var totalBufferLength = _numberOfLaserCamData * _outputBufferLength;
					_computeBuffers = new ComputeBuffer[BufferCount];
					for (var b = 0; b < BufferCount; b++)
					{
						_computeBuffers[b] = new ComputeBuffer(totalBufferLength, sizeof(float));
					}
				}

				// Register with SensorRenderManager for budget-managed scheduling.
				// For rasterization: each ExecuteRenderStep renders all sub-cameras.
				// For URT: single-step dispatch (IsURT=true).
				_nextRenderTime = Time.realtimeSinceStartup + 0.1f; // delayed start
				SensorRenderManager.Instance?.Register(this);

				if (!_useURT && _laserProcessThread != null)
				{
					_laserProcessThread.Start();
				}

				// Cache parent model renderers to hide the robot's own body during LiDAR rendering
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
					Debug.Log($"[Lidar] Found {_parentModelRenderers.Length} renderers in model '{modelName}' for self-occlusion exclusion (target layer={SelfOcclusionLayer})");
				}
			}
		}

		/// <summary>
		/// Walk up the transform hierarchy to find the closest parent tagged "Model"
		/// and return all Renderer components in its children. These will be moved
		/// to "Ignore Raycast" layer during LiDAR rendering to prevent the robot
		/// from seeing its own body.
		/// </summary>
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

		private void HideParentModelFromLidar()
		{
			if (_parentModelRenderers == null)
				return;
			for (var i = 0; i < _parentModelRenderers.Length; i++)
			{
				if (_parentModelRenderers[i] != null)
				{
					_parentModelOriginalLayers[i] = _parentModelRenderers[i].gameObject.layer;
					_parentModelRenderers[i].gameObject.layer = SelfOcclusionLayer;
				}
			}
		}

		private void RestoreParentModelVisibility()
		{
			if (_parentModelRenderers == null)
				return;
			for (var i = 0; i < _parentModelRenderers.Length; i++)
			{
				if (_parentModelRenderers[i] != null)
					_parentModelRenderers[i].gameObject.layer = _parentModelOriginalLayers[i];
			}
		}

		/// <summary>
		/// Initialize Unified Ray Tracing if available (hardware or compute backend).
		/// Falls back to rasterization if not available.
		/// </summary>
		private void InitURT()
		{
			var urtManager = URTSensorManager.Instance;
			if (urtManager == null || !urtManager.IsSupported)
			{
				Debug.Log($"[Lidar:{DeviceName}] Unified RT not available -- using rasterized sub-cameras");
				return;
			}

			var shaderAsset = Resources.Load<ComputeShader>("Shader/URTLidarRaycast");
			if (shaderAsset == null)
			{
				Debug.Log($"[Lidar:{DeviceName}] URTLidarRaycast shader not found -- using rasterized sub-cameras");
				return;
			}

			_urtShader = urtManager.CreateShader(shaderAsset);
			if (_urtShader == null)
			{
				Debug.Log($"[Lidar:{DeviceName}] Failed to create URT shader -- using rasterized sub-cameras");
				return;
			}

			var hSamples = (int)_horizontal.samples;
			var vSamples = (int)_vertical.samples;
			var totalSamples = hSamples * vSamples;

			_urtOutputBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalSamples, sizeof(float));
			_urtCmd = new CommandBuffer { name = "LidarURT" };

			// Pre-allocate scratch buffer for trace dispatch
			var scratchSize = _urtShader.GetTraceScratchBufferRequiredSizeInBytes((uint)hSamples, (uint)vSamples, 1);
			if (scratchSize > 0)
			{
				_urtScratchBuffer = new GraphicsBuffer(
					UnityEngine.Rendering.UnifiedRayTracing.RayTracingHelper.ScratchBufferTarget,
					(int)((scratchSize + 3) / 4), 4);
			}

			// Get self-exclusion mask from URTSensorManager
			_urtInclusionMask = urtManager.GetLidarInclusionMask(transform);

			// Configure static params via command buffer then execute
			var cmd = _urtCmd;
			cmd.Clear();
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_HAngleMax"), (float)_horizontal.angle.max);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_HAngleStep"), _resolution.angleH);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_VAngleMax"), (float)_vertical.angle.max);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_VAngleStep"), _resolution.angleV);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_HSamples"), hSamples);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_VSamples"), vSamples);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_RangeMin"), _scanRange.min);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_RangeMax"), _scanRange.max);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_RangeLinearResolution"), _resolution.linear);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_InstanceInclusionMask"), (int)_urtInclusionMask);
			Graphics.ExecuteCommandBuffer(cmd);

			_useURT = true;
			Debug.Log($"[Lidar:{DeviceName}] Unified RT enabled (backend: {urtManager.RTContext.BackendType}) -- {hSamples}x{vSamples} rays, inclusionMask=0x{_urtInclusionMask:X2}");
		}

		private void StartLaserCaptureDelayed()
		{
			if (_startLaserWork)
			{
				if (_useURT)
					StartCoroutine(CaptureLaserURT());
				else
					StartCoroutine(CaptureLaserCamera());
			}
		}

		private IEnumerator WaitStartSequence()
		{
			var lidarSequence = _globalSequence++;
			for (var i = 0; i < lidarSequence; i++)
				yield return null;
		}

		/// <summary>
		/// Unified RT capture coroutine. Replaces the entire sub-camera pipeline
		/// with a single shader dispatch. No sub-cameras, no render graph
		/// overhead, no LaserProcessing thread stitching.
		/// Fires all rays in parallel -> async readback -> directly fills LaserScan.Ranges.
		/// </summary>
		private IEnumerator CaptureLaserURT()
		{
			yield return WaitStartSequence();

			var lastUpdateTime = 0f;
			var hSamples = (uint)_horizontal.samples;
			var vSamples = (uint)_vertical.samples;
			var urtManager = URTSensorManager.Instance;

			// Cache shader property IDs
			var idSensorOrigin = Shader.PropertyToID("_SensorOrigin");
			var idSensorToWorld = Shader.PropertyToID("_SensorToWorld");
			var idAccelStruct = "_AccelStruct";
			var idOutput = Shader.PropertyToID("_Output");

			while (_startLaserWork)
			{
				lastUpdateTime += Time.unscaledDeltaTime;
				if (lastUpdateTime < UpdatePeriod)
				{
					yield return null;
					continue;
				}

				lastUpdateTime -= UpdatePeriod;

				if (urtManager == null || urtManager.AccelStruct == null)
				{
					yield return null;
					continue;
				}

				var capturedTime = GetNextSyntheticTime();
				var sensorPose = new Pose(transform.position, transform.rotation);

				// Build command buffer for this frame
				var cmd = _urtCmd;
				cmd.Clear();

				var pos = transform.position;
				_urtShader.SetVectorParam(cmd, idSensorOrigin, new Vector4(pos.x, pos.y, pos.z, 0));
				_urtShader.SetMatrixParam(cmd, idSensorToWorld, transform.localToWorldMatrix);
				_urtShader.SetAccelerationStructure(cmd, idAccelStruct, urtManager.AccelStruct);
				_urtShader.SetBufferParam(cmd, idOutput, _urtOutputBuffer);

				_urtShader.Dispatch(cmd, _urtScratchBuffer, hSamples, vSamples, 1);
				Graphics.ExecuteCommandBuffer(cmd);

				// Async GPU readback
				var frameCapturedTime = capturedTime;
				var framePose = sensorPose;
				AsyncGPUReadback.Request(_urtOutputBuffer, (req) =>
				{
					if (req.hasError || !req.done)
					{
						Debug.LogWarning("[Lidar URT] GPU readback error");
						return;
					}

					ProcessURTOutput(req, frameCapturedTime, framePose);
				});

				yield return null;
			}
		}

		/// <summary>
		/// Process URT ray tracing output: copy distances directly to LaserScan.Ranges,
		/// apply noise and filters, then enqueue the message.
		/// No stitching needed -- all rays are already in the correct order.
		/// </summary>
		private void ProcessURTOutput(AsyncGPUReadbackRequest req, double capturedTime, Pose sensorPose)
		{
			var laserScanStamped = new messages.LaserScanStamped();
			laserScanStamped.Time = new messages.Time();
			laserScanStamped.Time.Set(capturedTime);
			laserScanStamped.Scan = _laserScan;

			var laserScan = laserScanStamped.Scan;
			laserScan.WorldPose.Position.Set(sensorPose.position);
			laserScan.WorldPose.Orientation.Set(sensorPose.rotation);

			var src = req.GetData<float>();
			var totalSamples = src.Length;

			// Copy URT distances directly to Ranges (already in CW order: max->min)
			for (var i = 0; i < totalSamples && i < laserScan.Ranges.Length; i++)
			{
				laserScan.Ranges[i] = src[i];
			}

			if (_noise != null)
			{
				_noise.Apply<double>(laserScan.Ranges);
			}

			if (_laserFilter != null)
			{
				_laserFilter.DoFilter(ref laserScan);
			}

			if (OnLidarDataGenerated != null)
			{
				OnLidarDataGenerated.Invoke(laserScanStamped);
			}

			EnqueueMessage(laserScanStamped);
		}

		/// <summary>
		/// Independent lidar capture coroutine with async GPU readback.
		/// Renders all sub-cameras in a tight loop each frame (no yielding between them),
		/// then issues async GPU readback to avoid blocking the main thread.
		/// With 90deg HFOV (4 sub-cameras), total render cost is ~2-3ms per scan.
		/// </summary>
		private IEnumerator CaptureLaserCamera()
		{
			yield return WaitStartSequence();

			var axisRotation = Vector3.zero;
			var outputs = new LaserData.Output[_numberOfLaserCamData];
			var lastUpdateTime = 0f;

			while (_startLaserWork)
			{
				var now = Time.realtimeSinceStartup;
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
				var currentBuffer = _computeBuffers[_bufferIndex];

				// Reset outputs
				for (var i = 0; i < _numberOfLaserCamData; i++)
				{
					outputs[i] = new LaserData.Output(i);
				}

				// Hide the robot's own body so the LiDAR doesn't see it
				HideParentModelFromLidar();

				// Render all sub-cameras in tight loop
				for (var dataIndex = 0; dataIndex < _numberOfLaserCamData; dataIndex++)
				{
					if (!_camControlInfo[dataIndex].isOverlappingDirection)
					{
						continue;
					}

					outputs[dataIndex] = new LaserData.Output(dataIndex, _outputBufferLength);

					axisRotation.y = _camControlInfo[dataIndex].laserCamRotationalAngle;
					_laserCam.transform.localRotation = Quaternion.Euler(axisRotation);

					using (s_LidarSubCamRenderMarker.Auto())
					{
						_laserCam.Render();
					}

					using (s_LidarComputeMarker.Auto())
					{
						if (_laserCompute != null)
						{
							_laserCompute.SetInt("_DataOffset", dataIndex * _outputBufferLength);
							_laserCompute.SetTexture(_laserComputeKernel, "_DepthTexture", _laserCam.targetTexture);
							_laserCompute.SetBuffer(_laserComputeKernel, "_RayData", currentBuffer);
							_laserCompute.Dispatch(_laserComputeKernel, _laserComputeGroupsX, _laserComputeGroupsY, 1);
						}
					}
					_laserCam.enabled = false;
				}

				// Restore parent model visibility after all sub-cameras have rendered
				RestoreParentModelVisibility();

				// Async GPU readback -- capture locals for closure
				var framePose = sensorPose;
				var frameCapturedTime = capturedTime;
				var frameOutputs = new LaserData.Output[_numberOfLaserCamData];
				for (var i = 0; i < _numberOfLaserCamData; i++)
				{
					frameOutputs[i] = new LaserData.Output(
						outputs[i].dataIndex,
						outputs[i].rayData != null ? outputs[i].rayData.Length : 0);
				}

				AsyncGPUReadback.Request(currentBuffer, (req) =>
				{
					if (req.hasError || !req.done)
					{
						Debug.LogWarning("Lidar GPU readback error");
						return;
					}

					var src = req.GetData<float>();
					for (var i = 0; i < _numberOfLaserCamData; i++)
					{
						if (frameOutputs[i].rayData == null)
							continue;
						frameOutputs[i].ConvertDataType(src);
					}

					_outputQueue.Enqueue((frameCapturedTime, framePose, frameOutputs));
					_dataAvailable.Set();
				});

				yield return null;
			}
		}

		// ═══════════════════════════════════════════════════════════════
		// ISensorRenderable implementation
		// ═══════════════════════════════════════════════════════════════

		/// <summary>
		/// URT path is a single cheap compute dispatch (URT).
		/// Rasterization path requires multiple sub-camera renders.
		/// </summary>
		public bool IsURT => _useURT;

		/// <summary>
		/// Phase-locked readiness check.
		/// </summary>
		public bool IsReadyToRender(float realtimeNow)
		{
			if (!_startLaserWork) return false;
			if (_laserCam == null) return false;
			return realtimeNow >= _nextRenderTime;
		}

		/// <summary>
		/// Urgency = how overdue this LiDAR is (seconds past scheduled time).
		/// </summary>
		public float GetRenderUrgency(float realtimeNow)
		{
			return realtimeNow - _nextRenderTime;
		}

		/// <summary>
		/// Execute one render step of the LiDAR scan.
		/// For URT: single dispatch -> returns true immediately.
		/// For rasterization: renders all sub-cameras per call.
		///   Returns true when the scan finishes (triggers async readback).
		/// </summary>
		public bool ExecuteRenderStep(float realtimeNow)
		{
			if (_useURT)
			{
				return ExecuteURTStep(realtimeNow);
			}
			return ExecuteRasterStep(realtimeNow);
		}

		/// <summary>
		/// URT single-step: dispatch all rays, async readback, advance schedule.
		/// </summary>
		private bool ExecuteURTStep(float realtimeNow)
		{
			AdvanceLidarRenderSchedule(realtimeNow);

			var capturedTime = GetNextSyntheticTime();
			var sensorPose = new Pose(transform.position, transform.rotation);

			var hSamples = (uint)_horizontal.samples;
			var vSamples = (uint)_vertical.samples;
			var urtManager = URTSensorManager.Instance;

			if (urtManager == null || urtManager.AccelStruct == null)
				return true;

			var cmd = _urtCmd;
			cmd.Clear();

			var pos = transform.position;
			var idSensorOrigin = Shader.PropertyToID("_SensorOrigin");
			var idSensorToWorld = Shader.PropertyToID("_SensorToWorld");
			var idAccelStruct = "_AccelStruct";
			var idOutput = Shader.PropertyToID("_Output");

			_urtShader.SetVectorParam(cmd, idSensorOrigin, new Vector4(pos.x, pos.y, pos.z, 0));
			_urtShader.SetMatrixParam(cmd, idSensorToWorld, transform.localToWorldMatrix);
			_urtShader.SetAccelerationStructure(cmd, idAccelStruct, urtManager.AccelStruct);
			_urtShader.SetBufferParam(cmd, idOutput, _urtOutputBuffer);

			_urtShader.Dispatch(cmd, _urtScratchBuffer, hSamples, vSamples, 1);
			Graphics.ExecuteCommandBuffer(cmd);

			var frameCapturedTime = capturedTime;
			var framePose = sensorPose;
			AsyncGPUReadback.Request(_urtOutputBuffer, (req) =>
			{
				if (req.hasError || !req.done)
				{
					Debug.LogWarning("[Lidar URT] GPU readback error");
					return;
				}
				ProcessURTOutput(req, frameCapturedTime, framePose);
			});

			return true;
		}

		/// <summary>
		/// Rasterization single-step: renders ALL sub-cameras in one call.
		///
		/// This mirrors the coroutine's tight loop: render all sub-cameras,
		/// then issue a single async readback for the whole scan.
		/// Total GPU cost is ~2ms for 4 sub-cameras -- well within budget.
		///
		/// Single-step rendering is preferred over multi-step because:
		/// - The pipeline can reuse render state across sequential Camera.Render() calls
		/// - All sub-cameras share the same parent model hide/restore cycle
		/// - The SensorRenderManager can re-schedule the LiDAR within the
		///   same frame if it's overdue (e.g., 50 Hz target at 30 FPS)
		/// </summary>
		private bool ExecuteRasterStep(float realtimeNow)
		{
			var capturedTime = GetNextSyntheticTime();
			var sensorPose = new Pose(transform.position, transform.rotation);
			_bufferIndex = (_bufferIndex + 1) % BufferCount;
			var currentBuffer = _computeBuffers[_bufferIndex];

			// Allocate output array
			var outputs = new LaserData.Output[_numberOfLaserCamData];
			for (var i = 0; i < _numberOfLaserCamData; i++)
			{
				outputs[i] = new LaserData.Output(i);
			}

			HideParentModelFromLidar();

			// Render ALL sub-cameras in tight loop
			var axisRotation = Vector3.zero;
			for (var dataIndex = 0; dataIndex < _numberOfLaserCamData; dataIndex++)
			{
				if (!_camControlInfo[dataIndex].isOverlappingDirection)
					continue;

				outputs[dataIndex] = new LaserData.Output(dataIndex, _outputBufferLength);

				axisRotation.y = _camControlInfo[dataIndex].laserCamRotationalAngle;
				_laserCam.transform.localRotation = Quaternion.Euler(axisRotation);

				using (s_LidarSubCamRenderMarker.Auto())
				{
					_laserCam.Render();
				}

				using (s_LidarComputeMarker.Auto())
				{
					if (_laserCompute != null)
					{
						_laserCompute.SetInt("_DataOffset", dataIndex * _outputBufferLength);
						_laserCompute.SetTexture(_laserComputeKernel, "_DepthTexture", _laserCam.targetTexture);
						_laserCompute.SetBuffer(_laserComputeKernel, "_RayData", currentBuffer);
						_laserCompute.Dispatch(_laserComputeKernel, _laserComputeGroupsX, _laserComputeGroupsY, 1);
					}
				}
				_laserCam.enabled = false;
			}

			RestoreParentModelVisibility();
			AdvanceLidarRenderSchedule(realtimeNow);

			// Async GPU readback -- capture locals for closure
			var framePose = sensorPose;
			var frameCapturedTime = capturedTime;
			var frameOutputs = new LaserData.Output[_numberOfLaserCamData];
			for (var i = 0; i < _numberOfLaserCamData; i++)
			{
				frameOutputs[i] = new LaserData.Output(
					outputs[i].dataIndex,
					outputs[i].rayData != null ? outputs[i].rayData.Length : 0);
			}

			AsyncGPUReadback.Request(currentBuffer, (req) =>
			{
				if (req.hasError || !req.done)
				{
					Debug.LogWarning("Lidar GPU readback error");
					return;
				}

				var src = req.GetData<float>();
				for (var i = 0; i < _numberOfLaserCamData; i++)
				{
					if (frameOutputs[i].rayData == null)
						continue;
					frameOutputs[i].ConvertDataType(src);
				}

				_outputQueue.Enqueue((frameCapturedTime, framePose, frameOutputs));
				_dataAvailable.Set();
			});

			return true; // Always single-step
		}

		/// <summary>
		/// Phase-locked schedule advancement for LiDAR.
		/// Same logic as Camera.AdvanceRenderSchedule.
		/// </summary>
		private void AdvanceLidarRenderSchedule(float realtimeNow)
		{
			_nextRenderTime += UpdatePeriod;
			// Cap max overdue backlog to 3 periods to prevent runaway burst,
			// but preserve 2 periods of debt so the catch-up pass can fire
			// 2-3 times per frame after a spike.
			if (_nextRenderTime < realtimeNow - UpdatePeriod * 3f)
				_nextRenderTime = realtimeNow - UpdatePeriod * 2f;
		}

		protected new void OnDestroy()
		{
			// Unregister from SensorRenderManager
			SensorRenderManager.Instance?.Unregister(this);

			_outputQueue.Clear();
			_startLaserWork = false;
			_dataAvailable.Set();

			// Clean up compute buffers
			if (_computeBuffers != null)
			{
				foreach (var buf in _computeBuffers)
					buf?.Release();
				_computeBuffers = null;
			}

			if (_laserProcessThread != null && _laserProcessThread.IsAlive)
			{
				_laserProcessThread.Join();
			}

			StopAllCoroutines();

			if (_cb != null)
			{
				if (_laserCam != null)
					_laserCam.RemoveCommandBuffer(CameraEvent.AfterEverything, _cb);
				_cb.Release();
				_cb = null;
			}

			if (_laserCam != null)
			{
				try
				{
					_laserCam.RemoveAllCommandBuffers();
				}
				catch
				{
					Debug.LogWarning("Failed to RemoveAllCommandBuffers");
				}
			}

			if (_depthMaterial != null)
			{
				Destroy(_depthMaterial);
				_depthMaterial = null;
			}

			// Clean up URT resources
			_urtOutputBuffer?.Release();
			_urtOutputBuffer = null;
			_urtScratchBuffer?.Release();
			_urtScratchBuffer = null;
			_urtCmd?.Release();
			_urtCmd = null;
			_urtShader = null;

			_rtHandle?.Release();
			Destroy(_laserCompute);
			_laserCompute = null;

			base.OnDestroy();
		}

		protected override void InitializeMessages()
		{
			_laserScan = new messages.LaserScan();
			_laserScan.WorldPose = new messages.Pose();
			_laserScan.WorldPose.Position = new messages.Vector3d();
			_laserScan.WorldPose.Orientation = new messages.Quaternion();
		}

		protected override void SetupMessages()
		{
			if (_vertical.Equals(default(LaserData.Scan)))
			{
				_vertical = new LaserData.Scan(1);
			}

			_laserScan.Frame = DeviceName;
			_laserScan.Count = _horizontal.samples;
			_laserScan.AngleMin = _horizontal.angle.min * Mathf.Deg2Rad;
			_laserScan.AngleMax = _horizontal.angle.max * Mathf.Deg2Rad;
			_laserScan.AngleStep = _horizontal.angleStep * Mathf.Deg2Rad;

			_laserScan.RangeMin = _scanRange.min;
			_laserScan.RangeMax = _scanRange.max;

			_laserScan.VerticalCount = _vertical.samples;
			_laserScan.VerticalAngleMin = _vertical.angle.min * Mathf.Deg2Rad;
			_laserScan.VerticalAngleMax = _vertical.angle.max * Mathf.Deg2Rad;
			_laserScan.VerticalAngleStep = _vertical.angleStep * Mathf.Deg2Rad;

			_resolution.angleH = (float)_horizontal.angleStep;
			_resolution.angleV = (float)_vertical.angleStep;

			var totalSamples = _laserScan.Count * _laserScan.VerticalCount;

			// Debug.Log(_laserScan.VerticalCount + ", " + _laserScan.VerticalAngleMin + ", " + _laserScan.VerticalAngleMax + ", " + _laserScan.VerticalAngleStep);
			// Debug.Log(_laserScan.Count + " x " + _laserScan.VerticalCount + " = " + totalSamples);
			// Debug.Log($"angle step: deg H:{_horizontal.angleStep} V:{_vertical.angleStep}, rad H:{_laserScan.AngleStep} V:{_laserScan.VerticalAngleStep}");
			// Debug.Log($"linear: {_resolution.linear} angle H: {_resolution.angleH} V: {_resolution.angleV}");

			_laserScan.Ranges = new double[totalSamples];
			_laserScan.Intensities = new double[totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, 0.0);
		}

		private void SetupLaserCamera()
		{
			LaserCameraHFov = (_vertical.samples > 1) ? HFOV_FOR_3D_LIDAR : HFOV_FOR_2D_LIDAR;
			LaserCameraHFovHalf = LaserCameraHFov * 0.5f;

			// Original VFOV = lidar's actual vertical angle range (before expansion)
			LaserCameraVFovOriginal = (_vertical.samples == 1) ? 1 : (Mathf.Max(Mathf.Abs(_vertical.angle.min), Mathf.Abs(_vertical.angle.max)) * 2);

			if (_vertical.samples == 1)
			{
				LaserCameraVFov = 1;
			}
			else
			{
				// Expand VFOV to account for keystone distortion at sub-camera edges.
				// At the horizontal edge (hAngle = HFOV/2), a vertical ray at elevation e
				// projects to atan(tan(e)/cos(HFOV/2)) on the image plane.
				var maxElevAbs = LaserCameraVFovOriginal * 0.5f;
				var cosHalfHFov = Mathf.Cos(LaserCameraHFovHalf * Mathf.Deg2Rad);
				var expandedHalfVFov = Mathf.Atan(Mathf.Tan(maxElevAbs * Mathf.Deg2Rad) / cosHalfHFov) * Mathf.Rad2Deg;
				LaserCameraVFov = Mathf.Max(expandedHalfVFov * 2f, 1f);
			}

			_laserCam.ResetWorldToCameraMatrix();
			_laserCam.ResetProjectionMatrix();

			_laserCam.allowHDR = false;
			_laserCam.allowMSAA = false;
			_laserCam.allowDynamicResolution = false;
			_laserCam.useOcclusionCulling = false;
			_laserCam.usePhysicalProperties = false;
			_laserCam.stereoTargetEye = StereoTargetEyeMask.None;
			_laserCam.orthographic = false;
			_laserCam.nearClipPlane = _scanRange.min;
			_laserCam.farClipPlane = _scanRange.max;
			_laserCam.cullingMask = LayerMask.GetMask("Default", "Plane");
			_laserCam.clearFlags = CameraClearFlags.Depth;
			_laserCam.depthTextureMode = DepthTextureMode.Depth;
			_laserCam.renderingPath = RenderingPath.Forward;

			var renderTextureWidth = Mathf.CeilToInt(LaserCameraHFov / _resolution.angleH);
			var renderTextureHeight = Mathf.CeilToInt(LaserCameraVFov / _resolution.angleV);
			// Debug.Log($"SetupLaserCamera: {_resolution.linear} {LaserCameraHFov} {_resolution.angleH} {LaserCameraVFov} {_resolution.angleV}, {renderTextureWidth} {renderTextureHeight}");

			RTHandles.SetHardwareDynamicResolutionState(false);
			_rtHandle?.Release();
			_rtHandle = RTHandles.Alloc(
				width: renderTextureWidth,
				height: renderTextureHeight,
				slices: 1,
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
				name: "RT_LidarDepthTexture");

			_laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = SensorHelper.MakeProjectionMatrixPerspective(LaserCameraHFov, LaserCameraVFov, _laserCam.nearClipPlane, _laserCam.farClipPlane);
			_laserCam.projectionMatrix = projMatrix;

			// Configure URP-specific camera data
			var universalLaserCamData = _laserCam.GetUniversalAdditionalCameraData();
			universalLaserCamData.renderShadows = false;
			universalLaserCamData.stopNaN = true;
			universalLaserCamData.dithering = true;
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

			// Depth capture via command buffer: blit camera depth through DepthRange shader
			var depthShader = Shader.Find("Sensor/DepthRange");
			_depthMaterial = new Material(depthShader);
			_depthMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

			_cb = new CommandBuffer();
			_cb.ClearRenderTarget(true, true, Color.clear);
			var tempTextureId = Shader.PropertyToID("_RenderCameraDepthTexture");
			_cb.GetTemporaryRT(tempTextureId, -1, -1);
			_cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			_cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, _depthMaterial);
			_cb.ReleaseTemporaryRT(tempTextureId);

			_laserCam.AddCommandBuffer(CameraEvent.AfterEverything, _cb);

			// _laserCam.hideFlags |= HideFlags.NotEditable;

			if (_noise != null)
			{
				_noise.SetCustomNoiseParameter(_noiseParamInRawXml);
				_noise.SetClampMin(_scanRange.min);
				_noise.SetClampMax(_scanRange.max);
			}
		}

		private void SetupLaserCameraData()
		{
			var LaserCameraVFovHalf = LaserCameraVFov * 0.5f; // expanded half (for texture pixel mapping)
			var LaserCameraVFovOriginalHalf = LaserCameraVFovOriginal * 0.5f; // lidar's actual half
			var LaserCameraRotationAngle = LaserCameraHFov;

			_numberOfLaserCamData = Mathf.CeilToInt(DEG360 / LaserCameraRotationAngle);
			var isEven = (_numberOfLaserCamData % 2 == 0) ? true : false;

			var targetDepthRT = _laserCam.targetTexture;
			var texWidth = targetDepthRT.width;
			var texHeight = targetDepthRT.height;
			// Dispatch dimensions = lidar's actual sample counts (not expanded texture size)
			var width = texWidth; // horizontal: no expansion, same as texture
			var height = (int)_vertical.samples; // vertical: lidar's actual channel count
			var centerAngleOffset = (_horizontal.angle.min < 0) ? (isEven ? -LaserCameraHFovHalf : 0) : LaserCameraHFovHalf;

			var scanCenter = (_horizontal.angle.min + _horizontal.angle.max) * 0.5f;
			var scanHalfFov = (_horizontal.angle.max - _horizontal.angle.min) * 0.5f;

			_camControlInfo = new LaserData.CameraControlInfo[_numberOfLaserCamData];
			for (var index = 0; index < _numberOfLaserCamData; index++)
			{
				var centerAngle = LaserCameraRotationAngle * index + centerAngleOffset;
				_camControlInfo[index].laserCamRotationalAngle = centerAngle;

				var angleDiff = Mathf.Abs(Mathf.DeltaAngle(scanCenter, centerAngle));
				var isOverlapping = angleDiff <= (scanHalfFov + LaserCameraHFovHalf);
				_camControlInfo[index].isOverlappingDirection = isOverlapping;
			}

			_laserComputeKernel = _laserCompute.FindKernel("ComputeLaserData");
			_laserCompute.GetKernelThreadGroupSizes(_laserComputeKernel, out var threadX, out var threadY, out var threadZ);
			// Debug.Log($"ComputeLaserData: THREADS_X Y Z = {threadX}, {threadY}, {threadZ}");

			_laserCompute.SetInt("_Width", width);
			_laserCompute.SetInt("_Height", height);
			_laserCompute.SetInt("_TexWidth", texWidth);
			_laserCompute.SetInt("_TexHeight", texHeight);
			_laserCompute.SetFloat("_MaxHAngleHalf", LaserCameraHFovHalf);
			_laserCompute.SetFloat("_MaxVAngleHalf", LaserCameraVFovOriginalHalf); // lidar's actual half-elevation
			_laserCompute.SetFloat("_MaxHAngleHalfTanInv", 1f / Mathf.Tan(LaserCameraHFovHalf * Mathf.Deg2Rad));
			_laserCompute.SetFloat("_MaxVAngleHalfTanInv", 1f / Mathf.Tan(LaserCameraVFovHalf * Mathf.Deg2Rad)); // expanded half for texture mapping
			_laserCompute.SetFloat("_RangeMin", _scanRange.min);
			_laserCompute.SetFloat("_RangeMax", _scanRange.max);
			_laserCompute.SetFloat("_RangeLinearResolution", _resolution.linear);
			_laserCompute.SetFloat("_AngleResH", _resolution.angleH);
			_laserCompute.SetFloat("_AngleResV", _resolution.angleV);

			_horizontalBufferLength = width;
			_laserComputeGroupsX = Mathf.CeilToInt(width / (float)threadX);
			_laserComputeGroupsY = Mathf.CeilToInt(height / (float)threadY);

			_parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = _numberOfLaserCamData };

			_outputBufferLength = width * height;
		}

		public void SetupLaserAngleFilter(in double filterAngleLower, in double filterAngleUpper, in bool useIntensity = false)
		{
			if (_laserFilter == null)
			{
				_laserFilter = new LaserFilter(_laserScan, useIntensity);
			}

			_laserFilter.SetupAngleFilter(filterAngleLower, filterAngleUpper);
		}

		public void SetupLaserRangeFilter(in double filterRangeMin, in double filterRangeMax, in bool useIntensity = false)
		{
			if (_laserFilter == null)
			{
				_laserFilter = new LaserFilter(_laserScan, useIntensity);
			}

			_laserFilter.SetupRangeFilter(filterRangeMin, filterRangeMax);
		}

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

		private void LaserProcessing()
		{
			const int BufferUnitSize = sizeof(double);

			var laserSamplesH = (int)_horizontal.samples;
			var laserStartAngleH = _horizontal.angle.min;
			var laserEndAngleH = _horizontal.angle.max;
			var laserTotalAngleH = _horizontal.angle.range;

			var dividedLaserTotalAngleH = 1 / laserTotalAngleH;
			var srcBufferHorizontalLength = _horizontalBufferLength;
			var dividedDataTotalAngleH = 1 / LaserCameraHFov;

			var laserSamplesV = (int)_vertical.samples;
			var laserSamplesVTotal = Mathf.CeilToInt(LaserCameraVFovOriginal * _vertical.samples / _vertical.angle.range);
			var isMaxAngleDominant = Mathf.Abs(_vertical.angle.max) > Mathf.Abs(_vertical.angle.min);
			var laserSamplesVStart = isMaxAngleDominant ? (laserSamplesVTotal - laserSamplesV) : 0;
			var laserSamplesVEnd = isMaxAngleDominant ? laserSamplesVTotal : laserSamplesV;

			// Debug.Log($"laserSamplesVTotal: {laserSamplesVTotal}, " +
			// 			$"isMaxAngleDominant: {isMaxAngleDominant}, " +
			// 			$"laserSamplesVStart: {laserSamplesVStart}, " +
			// 			$"laserSamplesVEnd: {laserSamplesVEnd} " +
			// 			$"laserScan.Ranges: {_laserScan.Ranges.LongLength}");

			(double capturedTime, Pose sensorWorldPose, LaserData.Output[] outputs) item;

			while (_startLaserWork)
			{
				if (_outputQueue.TryDequeue(out item))
				{
					using (s_LidarProcessMarker.Auto())
					{
						var laserScanStamped = new messages.LaserScanStamped();
						laserScanStamped.Time = new messages.Time();
						laserScanStamped.Time.Set(item.capturedTime);

						laserScanStamped.Scan = _laserScan;

						var laserScan = laserScanStamped.Scan;
						laserScan.WorldPose.Position.Set(item.sensorWorldPose.position);
						laserScan.WorldPose.Orientation.Set(item.sensorWorldPose.rotation);

						Array.Fill(laserScan.Ranges, double.NaN);

						Parallel.For(0, _numberOfLaserCamData, _parallelOptions, index =>
						{
							var srcBuffer = item.outputs[index].rayData;
							if (srcBuffer == null)
							{
								return;
							}

							var dataStartAngleH = _camControlInfo[index].laserCamRotationalAngle - LaserCameraHFovHalf;
							var dataEndAngleH = _camControlInfo[index].laserCamRotationalAngle + LaserCameraHFovHalf;

							if (laserStartAngleH < 0 && dataEndAngleH > DEG180)
							{
								dataStartAngleH -= DEG360;
								dataEndAngleH -= DEG360;
							}

							var dstSampleIndexV = 0;
							for (var srcSampleIndexV = laserSamplesVStart; srcSampleIndexV < laserSamplesVEnd; srcSampleIndexV++)
							{
								var srcBufferOffset = 0;
								var dstBufferOffset = 0;
								var copyLength = 0;
								var doCopy = true;

								if (dataStartAngleH <= laserStartAngleH && laserStartAngleH < dataEndAngleH) // start side
								{
									if (laserEndAngleH >= dataEndAngleH)
									{
										var dataCopyLengthRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
										copyLength = srcBufferHorizontalLength - Mathf.CeilToInt(srcBufferHorizontalLength * dataCopyLengthRatio);
										srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV;
										dstBufferOffset = laserSamplesH * dstSampleIndexV + (laserSamplesH - copyLength);
									}
									else
									{
										var dataCopyLengthRatio = (laserEndAngleH - laserStartAngleH) * dividedDataTotalAngleH;
										var startBufferOffsetRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
										copyLength = Mathf.FloorToInt(srcBufferHorizontalLength * dataCopyLengthRatio) - 1;
										srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV + Mathf.CeilToInt(srcBufferHorizontalLength * startBufferOffsetRatio);
										dstBufferOffset = laserSamplesH * dstSampleIndexV;
									}
								}
								else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH) // middle
								{
									copyLength = srcBufferHorizontalLength;
									var bufferLengthRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
									srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV;
									dstBufferOffset = Mathf.CeilToInt(laserSamplesH * (dstSampleIndexV + 1 - bufferLengthRatio)) - copyLength;
								}
								else if (dataStartAngleH > laserStartAngleH && laserEndAngleH >= dataStartAngleH) // end side
								{
									var dataCopyLengthRatio = Mathf.Abs(laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
									copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * dataCopyLengthRatio);
									srcBufferOffset = srcBufferHorizontalLength * (srcSampleIndexV + 1) - copyLength;
									dstBufferOffset = laserSamplesH * dstSampleIndexV;
								}
								else
								{
									doCopy = false;
								}

								if (doCopy && copyLength > 0 &&
									srcBufferOffset >= 0 && dstBufferOffset >= 0 &&
									srcBufferOffset + copyLength <= srcBuffer.Length &&
									dstBufferOffset + copyLength <= laserScan.Ranges.Length)
								{
									try
									{
										Buffer.BlockCopy(srcBuffer, srcBufferOffset * BufferUnitSize,
														laserScan.Ranges, dstBufferOffset * BufferUnitSize,
														copyLength * BufferUnitSize);
									}
									catch (Exception ex)
									{
										Debug.LogWarning(
											$"[BufferCopyError] {ex.Message}\n" +
											$"idx={index} srcVidx={srcSampleIndexV} dstVidx={dstSampleIndexV}\n" +
											$"srcTotalLen={srcBuffer.Length} dstTotalLen={laserScan.Ranges.Length}\n" +
											$"srcOffset={srcBufferOffset} dstOffset={dstBufferOffset} copyLen={copyLength}\n" +
											$"srcOffsetEnd={srcBufferOffset + copyLength} dstOffsetEnd={dstBufferOffset + copyLength}"
										);
									}
								}

								dstSampleIndexV++;
							}
						});

						if (_noise != null)
						{
							var ranges = laserScan.Ranges;
							_noise.Apply<double>(ranges);
						}

						if (_laserFilter != null)
						{
							_laserFilter.DoFilter(ref laserScan);
						}

						if (OnLidarDataGenerated != null)
						{
							OnLidarDataGenerated.Invoke(laserScanStamped);
						}

						EnqueueMessage(laserScanStamped);

#if UNITY_EDITOR
						UpdateProfiler("LIDAR", _laserScan.Count * _laserScan.VerticalCount * sizeof(double) * 2);
#endif
					}
				}
				else
				{
					_dataAvailable.WaitOne();
				}
			}
		}

		[SerializeField] private static int _indexForVisualize = 0;
		[SerializeField] private static int _maxCountForVisualize = 3;
		[SerializeField] private static float _hueOffsetForVisualize = 0f;
		[SerializeField] private const float UnitHueOffsetForVisualize = 0.07f;
		[SerializeField] private const float AlphaForVisualize = 0.75f;

		protected override IEnumerator OnVisualize()
		{
			var visualizer = new GameObject("__laser_visualizer__");
			visualizer.layer = LayerMask.NameToLayer("Visualization");
			visualizer.transform.SetParent(this.transform, false);

			var lineRenderer = visualizer.AddComponent<LineRenderer>();
			lineRenderer.positionCount = 0;
			lineRenderer.widthMultiplier = 0.001f;
			lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
			lineRenderer.material.hideFlags = HideFlags.DontUnloadUnusedAsset;
			lineRenderer.useWorldSpace = true;

			var waitForSeconds = new WaitForSeconds(UpdatePeriod);
			var startAngleH = _horizontal.angle.min;
			var startAngleV = _vertical.angle.max;
			var endAngleV = _vertical.angle.min;
			var angleRangeV = _vertical.angle.range;
			var horizontalSamples = _horizontal.samples;
			var rangeMin = _scanRange.min;
			var rangeMax = _scanRange.max;

			if (_indexForVisualize >= _maxCountForVisualize)
			{
				_indexForVisualize = 0;
				_hueOffsetForVisualize += UnitHueOffsetForVisualize;
			}
			var hue = ((float)_indexForVisualize++ / Mathf.Max(1, _maxCountForVisualize)) + _hueOffsetForVisualize;
			hue = (hue % 1f + 1f) % 1f;
			// Debug.Log($"hue{hue} _indexForVisualize{_indexForVisualize}");
			var baseRayColor = Color.HSVToRGB(hue, 0.9f, 1f);

			var positions = new List<Vector3>((int)(horizontalSamples * _vertical.samples) * 2);

			while (true)
			{
				var rangeData = GetRangeData();
				if (rangeData == null)
				{
					yield return waitForSeconds;
					continue;
				}

				positions.Clear();
				var rayStartBase = transform.position;
				var sensorWorldRotation = transform.rotation;

				for (var scanIndex = 0; scanIndex < rangeData.Count; scanIndex++)
				{
					var scanIndexH = scanIndex % horizontalSamples;
					var scanIndexV = scanIndex / horizontalSamples;

					var rayAngleH = startAngleH + (_resolution.angleH * scanIndexH);
					var rayAngleV = startAngleV - (_resolution.angleV * scanIndexV);

					var ccwIndex = (int)(rangeData.Count - scanIndex - 1);
					var rayData = (float)rangeData[ccwIndex];

					if (float.IsNaN(rayData) || rayData > rangeMax)
						continue;

					var t = Mathf.InverseLerp(endAngleV, startAngleV, rayAngleV);
					var s = Mathf.Lerp(0.55f, 0.95f, t);
					var rayColor = Color.HSVToRGB(hue, s, 0.95f);
					rayColor.a = AlphaForVisualize;

					var localAngles = Quaternion.AngleAxis(rayAngleH, Vector3.up) * Quaternion.AngleAxis(rayAngleV, -Vector3.right);
					var dir = sensorWorldRotation * localAngles * Vector3.forward;
					dir.Normalize();

					var start = rayStartBase + dir * rangeMin;
					var end = start + dir * (rayData - rangeMin);

					positions.Add(start);
					positions.Add(end);
				}

				lineRenderer.positionCount = positions.Count;
				lineRenderer.SetPositions(positions.ToArray());

				var baseColor = Color.HSVToRGB(hue, 0.9f, 1f);
				baseColor.a = AlphaForVisualize;
				lineRenderer.startColor = baseColor;
				lineRenderer.endColor = baseColor;

				yield return waitForSeconds;
			}
		}

		public IReadOnlyList<double> GetRangeData()
		{
			try
			{
				return Array.AsReadOnly(_laserScan.Ranges);
			}
			catch
			{
				return null;
			}
		}
	}
}
