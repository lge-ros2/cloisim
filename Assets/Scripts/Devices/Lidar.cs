/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device, ISensorRenderable
	{
		[SerializeField] private messages.LaserScan _laserScan = null;
		[SerializeField] private Thread _laserProcessThread = null;

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

		[Header("Processing")]
		private Transform _lidarLink = null;

		private bool _startLaserWork = false;

		private ConcurrentQueue<(double, Pose, float[])> _outputQueue = new();
		private readonly AutoResetEvent _dataAvailable = new(false);

		#region "Unified Ray Tracing (per-sensor resources)"
		private static ComputeShader ComputeShaderLidarRayTrace = null;
		private ComputeShader _csRayTrace = null;
		private IRayTracingShader _rtShader = null;
		private GraphicsBuffer _rtTraceScratchBuffer = null;
		private CommandBuffer _urtCmdBuffer = null;
		private ComputeBuffer _rangeOutputBuffer = null;

		private uint _totalSamples = 0;

		// Cached shader property IDs
		private static readonly int PID_RangeOutput = Shader.PropertyToID("_RangeOutput");
		private static readonly int PID_SamplesH = Shader.PropertyToID("_SamplesH");
		private static readonly int PID_SamplesV = Shader.PropertyToID("_SamplesV");
		private static readonly int PID_AngleMinH = Shader.PropertyToID("_AngleMinH");
		private static readonly int PID_AngleStepH = Shader.PropertyToID("_AngleStepH");
		private static readonly int PID_AngleMinV = Shader.PropertyToID("_AngleMinV");
		private static readonly int PID_AngleStepV = Shader.PropertyToID("_AngleStepV");
		private static readonly int PID_RangeMin = Shader.PropertyToID("_RangeMin");
		private static readonly int PID_RangeMax = Shader.PropertyToID("_RangeMax");
		private static readonly int PID_RangeLinearResolution = Shader.PropertyToID("_RangeLinearResolution");
		private static readonly int PID_SensorPosition = Shader.PropertyToID("_SensorPosition");
		private static readonly int PID_SensorRight = Shader.PropertyToID("_SensorRight");
		private static readonly int PID_SensorUp = Shader.PropertyToID("_SensorUp");
		private static readonly int PID_SensorForward = Shader.PropertyToID("_SensorForward");
		#endregion

		public static void LoadComputeShader()
		{
			if (ComputeShaderLidarRayTrace == null)
			{
				ComputeShaderLidarRayTrace = Resources.Load<ComputeShader>("Shader/LidarRayTrace");
			}
		}

		public static void UnloadComputeShader()
		{
			if (ComputeShaderLidarRayTrace != null)
			{
				Resources.UnloadAsset(ComputeShaderLidarRayTrace);
				ComputeShaderLidarRayTrace = null;
			}
			Resources.UnloadUnusedAssets();
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_lidarLink = transform.parent;
			_laserProcessThread = new Thread(() => LaserProcessing());
		}

		protected override void OnStart()
		{
			SetupURT();
			SetupNoiseLimits();

			_startLaserWork = true;

			SensorRenderManager.Register(this, initialDelay: 0.1f);

			if (_laserProcessThread != null)
			{
				_laserProcessThread.Start();
			}
		}

		protected new void OnDestroy()
		{
			UnloadComputeShader();

			_startLaserWork = false;
			_dataAvailable.Set();

			SensorRenderManager.Unregister(this);

			if (_laserProcessThread != null && _laserProcessThread.IsAlive)
			{
				_laserProcessThread.Join();
			}

			_outputQueue.Clear();

			// Clean up per-sensor URT resources
			_urtCmdBuffer?.Release();
			_urtCmdBuffer = null;

			_rtTraceScratchBuffer?.Dispose();
			_rtTraceScratchBuffer = null;

			_rangeOutputBuffer?.Release();
			_rangeOutputBuffer = null;

			if (_csRayTrace != null)
			{
				Destroy(_csRayTrace);
				_csRayTrace = null;
			}

			URTSensorManager.Unregister(GetInstanceID());

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

			_totalSamples = _laserScan.Count * _laserScan.VerticalCount;

			_laserScan.Ranges = new double[_totalSamples];
			_laserScan.Intensities = new double[_totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, double.NaN);
		}

		#region "URT Setup"

		private void SetupURT()
		{
			LoadComputeShader();

			if (!URTSensorManager.Register(GetInstanceID()))
			{
				Debug.LogError("[Lidar] Failed to register with URTSensorManager");
				return;
			}

			_csRayTrace = Instantiate(ComputeShaderLidarRayTrace);
			if (_csRayTrace == null)
			{
				Debug.LogError("[Lidar] Failed to instantiate LidarRayTrace compute shader");
				return;
			}

			_rtShader = URTSensorManager.CreateShader(_csRayTrace);

			var samplesH = _laserScan.Count;
			var samplesV = _laserScan.VerticalCount;

			_rangeOutputBuffer?.Release();
			_rangeOutputBuffer = new ComputeBuffer((int)_totalSamples, sizeof(float));

			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(
				_rtShader, samplesH, samplesV, 1);

			_urtCmdBuffer = new CommandBuffer { name = "Lidar URT Dispatch" };

			Debug.Log($"[Lidar] URT initialized, samples={samplesH}x{samplesV}={_totalSamples}, " +
				$"range=[{_scanRange.min:F2}, {_scanRange.max:F2}], " +
				$"hAngle=[{_horizontal.angle.min:F1}, {_horizontal.angle.max:F1}] deg");
		}

		private void SetupNoiseLimits()
		{
			if (_noise != null)
			{
				_noise.SetCustomNoiseParameter(_noiseParamInRawXml);
				_noise.SetClampMin(_scanRange.min);
				_noise.SetClampMax(_scanRange.max);
			}
		}

		/// <summary>Bind acceleration structure and output buffer to the shader.</summary>
		private void BindShaderResources(CommandBuffer cmd)
		{
			_rtShader.SetAccelerationStructure(cmd, "_AccelStruct", URTSensorManager.AccelStruct);
			_rtShader.SetBufferParam(cmd, PID_RangeOutput, _rangeOutputBuffer);
		}

		/// <summary>Static scan configuration — values never change after setup.</summary>
		private void SetScanConfigParams(CommandBuffer cmd, uint samplesH, uint samplesV)
		{
			_rtShader.SetIntParam(cmd, PID_SamplesH, (int)samplesH);
			_rtShader.SetIntParam(cmd, PID_SamplesV, (int)samplesV);

			_rtShader.SetFloatParam(cmd, PID_AngleMinH, (float)_laserScan.AngleMin);
			_rtShader.SetFloatParam(cmd, PID_AngleStepH, (float)_laserScan.AngleStep);
			_rtShader.SetFloatParam(cmd, PID_AngleMinV, (float)_laserScan.VerticalAngleMin);
			_rtShader.SetFloatParam(cmd, PID_AngleStepV, (float)_laserScan.VerticalAngleStep);

			_rtShader.SetFloatParam(cmd, PID_RangeMin, _scanRange.min);
			_rtShader.SetFloatParam(cmd, PID_RangeMax, _scanRange.max);
			_rtShader.SetFloatParam(cmd, PID_RangeLinearResolution, _resolution.linear);
		}

		/// <summary>Dynamic sensor pose — changes every frame.</summary>
		private void SetSensorPoseParams(CommandBuffer cmd, Vector3 position, Vector3 right, Vector3 up, Vector3 forward)
		{
			_rtShader.SetVectorParam(cmd, PID_SensorPosition, new Vector4(position.x, position.y, position.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_SensorRight, new Vector4(right.x, right.y, right.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_SensorUp, new Vector4(up.x, up.y, up.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_SensorForward, new Vector4(forward.x, forward.y, forward.z, 0f));
		}

		#endregion

		#region "ISensorRenderable"

		public bool IsURT => true;
		public float RenderPeriod => UpdatePeriod;

		public bool CanRender
		{
			get
			{
				if (!_startLaserWork) return false;
				if (_rtShader == null || _rangeOutputBuffer == null) return false;
				return true;
			}
		}

		public bool ExecuteRenderStep(float realtimeNow)
		{
			ExecuteRender();
			return true;
		}

		/// <summary>
		/// URT render path: cast rays on the GPU in a spherical pattern
		/// matching the lidar's configured scan angles. All rays are dispatched
		/// in a single compute pass — no camera rotation or multi-slice stitching needed.
		/// </summary>
		private void ExecuteRender()
		{
			if (URTSensorManager.AccelStruct == null || _rtShader == null)
				return;

			var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

			var sensorTransform = this.transform;
			var sensorPos = sensorTransform.position;
			var sensorRight = sensorTransform.right;
			var sensorUp = sensorTransform.up;
			var sensorForward = sensorTransform.forward;
			var sensorWorldPose = new Pose(sensorPos, sensorTransform.rotation);

			var samplesH = _laserScan.Count;
			var samplesV = _laserScan.VerticalCount;

			// Resize scratch buffer if needed
			RayTracingHelper.ResizeScratchBufferForTrace(_rtShader, samplesH, samplesV, 1, ref _rtTraceScratchBuffer);

			// === Record all GPU work into a single CommandBuffer ===
			_urtCmdBuffer.Clear();

			// 1. Shared BVH: scene gather, transform update, build (once per frame)
			URTSensorManager.EnsureBVHReady(_urtCmdBuffer);

			// 2. URT lidar ray trace dispatch
			BindShaderResources(_urtCmdBuffer);
			SetScanConfigParams(_urtCmdBuffer, samplesH, samplesV);
			SetSensorPoseParams(_urtCmdBuffer, sensorPos, sensorRight, sensorUp, sensorForward);

			_rtShader.Dispatch(_urtCmdBuffer, _rtTraceScratchBuffer, samplesH, samplesV, 1);

			// === Execute all recorded GPU work ===
			Graphics.ExecuteCommandBuffer(_urtCmdBuffer);

			// --- Async readback (non-blocking) ---
			AsyncGPUReadback.Request(_rangeOutputBuffer, (req) =>
			{
				if (req.hasError || !req.done)
				{
					Debug.LogWarning("[Lidar] Async GPU readback failed");
					return;
				}

				var src = req.GetData<float>();
				var rangeData = new float[src.Length];
				src.CopyTo(rangeData);

				_outputQueue.Enqueue((capturedTime, sensorWorldPose, rangeData));
				_dataAvailable.Set();
			});
		}

		#endregion

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

		/// <summary>
		/// Background thread: dequeues GPU readback results and assembles
		/// LaserScanStamped messages. The URT output buffer is already laid
		/// out as [vIndex * samplesH + hIndex] so no multi-camera stitching
		/// is needed — a simple copy converts float to double.
		/// </summary>
		private void LaserProcessing()
		{
			(double capturedTime, Pose sensorWorldPose, float[] rangeData) item;

			while (_startLaserWork)
			{
				if (_outputQueue.TryDequeue(out item))
				{
					var laserScanStamped = new messages.LaserScanStamped();
					laserScanStamped.Time = new messages.Time();
					laserScanStamped.Time.Set(item.capturedTime);

					laserScanStamped.Scan = _laserScan;

					var laserScan = laserScanStamped.Scan;
					laserScan.WorldPose.Position.Set(item.sensorWorldPose.position);
					laserScan.WorldPose.Orientation.Set(item.sensorWorldPose.rotation);

					// Direct copy: GPU output is already in the correct order
					var ranges = laserScan.Ranges;
					for (var i = 0; i < ranges.Length && i < item.rangeData.Length; i++)
					{
						ranges[i] = item.rangeData[i];
					}

					if (_noise != null)
					{
						_noise.Apply<double>(ranges);
					}

					if (_laserFilter != null)
					{
						_laserFilter.DoFilter(ref laserScan);
					}

					EnqueueMessage(laserScanStamped);

#if UNITY_EDITOR
					UpdateProfiler("LIDAR", _laserScan.Count * _laserScan.VerticalCount * sizeof(double) * 2);
#endif
				}
				else
				{
					_dataAvailable.WaitOne();
				}
			}
		}

		protected override IEnumerator OnVisualize()
		{
			var visualizer = new GameObject("__laser_visualizer__");
			visualizer.layer = LayerMask.NameToLayer("Visualization");
			visualizer.transform.SetParent(this.transform, false);

			if (_vertical.samples > 1)
			{
				yield return OnVisualizePointCloud(visualizer);
			}
			else
			{
				yield return OnVisualizeLines(visualizer);
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
