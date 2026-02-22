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
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private static int _globalSequence = 0;
		[SerializeField] private messages.LaserScan _laserScan = null;
		[SerializeField] private Thread _laserProcessThread = null;
		public Action<messages.LaserScanStamped> OnLidarDataGenerated;

		[SerializeField] private const float DEG180 = Mathf.PI * Mathf.Rad2Deg;
		[SerializeField] private const float DEG360 = DEG180 * 2;

		[SerializeField] private const float HFOV_FOR_2D_LIDAR = 90f;
		[SerializeField] private const float HFOV_FOR_3D_LIDAR = 10f;
		[SerializeField] private float LaserCameraHFov = 0f;
		[SerializeField] private float LaserCameraHFovHalf = 0;
		[SerializeField] private float LaserCameraVFov = 0;

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

				// Use Invoke to start coroutine outside WaitForEndOfFrame context
				// (coroutines started from WaitForEndOfFrame don't resume after yield)
				Invoke(nameof(StartLaserCaptureDelayed), 0.1f);

				if (_laserProcessThread != null)
				{
					_laserProcessThread.Start();
				}
			}
		}

		private void StartLaserCaptureDelayed()
		{
			if (_startLaserWork)
			{
				StartCoroutine(CaptureLaserCamera());
			}
		}

		protected new void OnDestroy()
		{
			_outputQueue.Clear();
			_startLaserWork = false;
			_dataAvailable.Set();

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
					Debug.LogWarning("Failed to remove RemoveAllCommandBuffers");
				}
			}

			if (_depthMaterial != null)
			{
				Destroy(_depthMaterial);
				_depthMaterial = null;
			}

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
			Array.Fill(_laserScan.Intensities, double.NaN);
		}

		private void SetupLaserCamera()
		{
			LaserCameraVFov = (_vertical.samples == 1) ? 1 : (Mathf.Max(Mathf.Abs(_vertical.angle.min), Mathf.Abs(_vertical.angle.max)) * 2);
			LaserCameraHFov = (_vertical.samples > 1) ? HFOV_FOR_3D_LIDAR : HFOV_FOR_2D_LIDAR;
			LaserCameraHFovHalf = LaserCameraHFov * 0.5f;

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

			// These APIs are only available with the built-in renderer, not HDRP/URP
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
			{
				_laserCam.stereoTargetEye = StereoTargetEyeMask.None;
				_laserCam.renderingPath = RenderingPath.Forward;
			}

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

			var hdLaserCamData = _laserCam.GetComponent<HDAdditionalCameraData>();
			if (hdLaserCamData == null)
			{
				hdLaserCamData = _laserCam.gameObject.AddComponent<HDAdditionalCameraData>();
			}

			// Optimize HDRP lidar camera — we only need depth, disable all expensive features
			hdLaserCamData.customRenderingSettings = true;
			// Disable shadows
			var shadowMask = hdLaserCamData.renderingPathCustomFrameSettingsOverrideMask;
			shadowMask.mask[(uint)FrameSettingsField.ShadowMaps] = true;
			shadowMask.mask[(uint)FrameSettingsField.ContactShadows] = true;
			shadowMask.mask[(uint)FrameSettingsField.ScreenSpaceShadows] = true;
			// Disable volumetrics and atmosphere
			shadowMask.mask[(uint)FrameSettingsField.Volumetrics] = true;
			shadowMask.mask[(uint)FrameSettingsField.ReprojectionForVolumetrics] = true;
			shadowMask.mask[(uint)FrameSettingsField.AtmosphericScattering] = true;
			// Disable post-processing and effects
			shadowMask.mask[(uint)FrameSettingsField.Postprocess] = true;
			shadowMask.mask[(uint)FrameSettingsField.SSAO] = true;
			shadowMask.mask[(uint)FrameSettingsField.SSR] = true;
			shadowMask.mask[(uint)FrameSettingsField.SubsurfaceScattering] = true;
			shadowMask.mask[(uint)FrameSettingsField.Refraction] = true;
			shadowMask.mask[(uint)FrameSettingsField.MotionVectors] = true;
			shadowMask.mask[(uint)FrameSettingsField.Decals] = true;
			shadowMask.mask[(uint)FrameSettingsField.TransparentObjects] = true;
			hdLaserCamData.renderingPathCustomFrameSettingsOverrideMask = shadowMask;

			var frameSettings = hdLaserCamData.renderingPathCustomFrameSettings;
			frameSettings.SetEnabled(FrameSettingsField.ShadowMaps, false);
			frameSettings.SetEnabled(FrameSettingsField.ContactShadows, false);
			frameSettings.SetEnabled(FrameSettingsField.ScreenSpaceShadows, false);
			frameSettings.SetEnabled(FrameSettingsField.Volumetrics, false);
			frameSettings.SetEnabled(FrameSettingsField.ReprojectionForVolumetrics, false);
			frameSettings.SetEnabled(FrameSettingsField.AtmosphericScattering, false);
			frameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
			frameSettings.SetEnabled(FrameSettingsField.SSAO, false);
			frameSettings.SetEnabled(FrameSettingsField.SSR, false);
			frameSettings.SetEnabled(FrameSettingsField.SubsurfaceScattering, false);
			frameSettings.SetEnabled(FrameSettingsField.Refraction, false);
			frameSettings.SetEnabled(FrameSettingsField.MotionVectors, false);
			frameSettings.SetEnabled(FrameSettingsField.Decals, false);
			frameSettings.SetEnabled(FrameSettingsField.TransparentObjects, false);
			hdLaserCamData.renderingPathCustomFrameSettings = frameSettings;

			var depthShader = Shader.Find("Sensor/DepthRange");
			_depthMaterial = new Material(depthShader);

			_cb = new CommandBuffer();
			_cb.ClearRenderTarget(true, true, Color.clear);
			var tempTextureId = Shader.PropertyToID("_RenderCameraDepthTexture");
			_cb.GetTemporaryRT(tempTextureId, -1, -1);
			_cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			_cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, _depthMaterial);
			_cb.ReleaseTemporaryRT(tempTextureId);

			// AddCommandBuffer is only available with the built-in renderer, not HDRP/URP
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
			{
				_laserCam.AddCommandBuffer(CameraEvent.AfterEverything, _cb);
			}

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
			var LaserCameraVFovHalf = LaserCameraVFov * 0.5f;
			var LaserCameraRotationAngle = LaserCameraHFov;

			_numberOfLaserCamData = Mathf.CeilToInt(DEG360 / LaserCameraRotationAngle);
			var isEven = (_numberOfLaserCamData % 2 == 0) ? true : false;

			var targetDepthRT = _laserCam.targetTexture;
			var width = targetDepthRT.width;
			var height = targetDepthRT.height;
			var centerAngleOffset = (_horizontal.angle.min < 0) ? (isEven ? -LaserCameraHFovHalf : 0) : LaserCameraHFovHalf;

			var scanCenter = (_horizontal.angle.min + _horizontal.angle.max) * 0.5f;
			var scanHalfFov = (_horizontal.angle.max - _horizontal.angle.min) * 0.5f;

			_camControlInfo = new LaserData.CameraControlInfo[_numberOfLaserCamData];
			for (var index = 0; index < _numberOfLaserCamData; index++)
			{
				var centerAngle = LaserCameraRotationAngle * index + centerAngleOffset;
				_camControlInfo[index].laserCamRotationalAngle = centerAngle;

				// var camMin = centerAngle - LaserCameraHFovHalf;
				// var camMax = centerAngle + LaserCameraHFovHalf;
				// var isOverlapping = (_horizontal.angle.min <= camMax) && (_horizontal.angle.max >= camMin);

				var angleDiff = Mathf.Abs(Mathf.DeltaAngle(scanCenter, centerAngle));
				var isOverlapping = angleDiff <= (scanHalfFov + LaserCameraHFovHalf);
				_camControlInfo[index].isOverlappingDirection = isOverlapping;
			}

			_laserComputeKernel = _laserCompute.FindKernel("ComputeLaserData");
			_laserCompute.GetKernelThreadGroupSizes(_laserComputeKernel, out var threadX, out var threadY, out var threadZ);
			// Debug.Log($"ComputeLaserData: THREADS_X Y Z = {threadX}, {threadY}, {threadZ}");

			_laserCompute.SetInt("_Width", width);
			_laserCompute.SetInt("_Height", height);
			_laserCompute.SetFloat("_MaxHAngleHalf", LaserCameraHFovHalf);
			_laserCompute.SetFloat("_MaxVAngleHalf", LaserCameraVFovHalf);
			_laserCompute.SetFloat("_MaxHAngleHalfTanInv", 1f / Mathf.Tan(LaserCameraHFovHalf * Mathf.Deg2Rad));
			_laserCompute.SetFloat("_MaxVAngleHalfTanInv", 1f / Mathf.Tan(LaserCameraVFovHalf * Mathf.Deg2Rad));
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

		private IEnumerator WaitStartSequence()
		{
			var lidarSequence = _globalSequence++;
			for (var i = 0; i < lidarSequence; i++)
				yield return null;
		}

		private IEnumerator CaptureLaserCamera()
		{
			yield return WaitStartSequence();

			var lidarSensorWorldPose = new Pose();
			var axisRotation = Vector3.zero;

			var outputs = new LaserData.Output[_numberOfLaserCamData];

			const int BufferCount = 5;
			var bufferIndex = 0;
			var totalBufferLength = _numberOfLaserCamData * _outputBufferLength;
			var computedBuffers = new ComputeBuffer[BufferCount];
			for (var b = 0; b < BufferCount; b++)
			{
				computedBuffers[b] = new ComputeBuffer(totalBufferLength, sizeof(float));
			}

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

				bufferIndex = (bufferIndex + 1) % BufferCount;
				var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

				lidarSensorWorldPose.position = transform.position;
				lidarSensorWorldPose.rotation = transform.rotation;

				var currentComputeBuffer = computedBuffers[bufferIndex];
				for (var dataIndex = 0; dataIndex < _numberOfLaserCamData; dataIndex++)
				{
					if (!_camControlInfo[dataIndex].isOverlappingDirection)
					{
						// Debug.Log($"Skip to render for {dataIndex} {_camControlInfo[dataIndex].laserCamRotationalAngle} {name}");
						outputs[dataIndex] = new LaserData.Output(dataIndex);
						continue;
					}
					else
					{
						outputs[dataIndex] = new LaserData.Output(dataIndex, _outputBufferLength);
					}

					axisRotation.y = _camControlInfo[dataIndex].laserCamRotationalAngle;

					_laserCam.transform.localRotation = Quaternion.Euler(axisRotation);
					_laserCam.Render();

					if (_laserCompute != null)
					{
						_laserCompute.SetInt("_DataOffset", dataIndex * _outputBufferLength);
						_laserCompute.SetTexture(_laserComputeKernel, "_DepthTexture", _laserCam.targetTexture);
						_laserCompute.SetBuffer(_laserComputeKernel, "_RayData", currentComputeBuffer);
						_laserCompute.Dispatch(_laserComputeKernel, _laserComputeGroupsX, _laserComputeGroupsY, 1);
					}
					_laserCam.enabled = false;
				}

				var framePose = lidarSensorWorldPose;
				var frameCapturedTime = capturedTime;
				var frameOutputs = new LaserData.Output[_numberOfLaserCamData];
				for (var i = 0; i < _numberOfLaserCamData; i++)
					frameOutputs[i] = new LaserData.Output(outputs[i].dataIndex, outputs[i].rayData != null ? outputs[i].rayData.Length : 0);

				AsyncGPUReadback.Request(currentComputeBuffer, (req) =>
					{
						if (req.hasError || !req.done)
						{
							Debug.LogWarning($"GPU readback not ready");
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

			foreach (var computeBuffer in computedBuffers)
				computeBuffer.Release();
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
			var laserSamplesVTotal = Mathf.CeilToInt(LaserCameraVFov * _vertical.samples / _vertical.angle.range);
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
									// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataCopyLengthRatio {4:F6} copyLength{5} srcBufferOffset {6} dstBufferOffset {7}",
									// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataCopyLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
								}
								else
								{
									var dataCopyLengthRatio = (laserEndAngleH - laserStartAngleH) * dividedDataTotalAngleH;
									var startBufferOffsetRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
									copyLength = Mathf.FloorToInt(srcBufferHorizontalLength * dataCopyLengthRatio) - 1;
									srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV + Mathf.CeilToInt(srcBufferHorizontalLength * startBufferOffsetRatio);
									dstBufferOffset = laserSamplesH * dstSampleIndexV;
									// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4:F6} dataCopyLengthRatio{5} srcBufferOffset {6} dstBufferOffset {7} startBufferOffsetRatio{8} index{9} srcBufferHorizontalLength{10} srcBuffer.Length{11}" ,
									// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataLengthRatio, dataCopyLengthRatio, srcBufferOffset, dstBufferOffset, startBufferOffsetRatio, index, srcBufferHorizontalLength, srcBuffer.Length);
								}
							}
							else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH) // middle
							{
								copyLength = srcBufferHorizontalLength;
								var bufferLengthRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
								srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV;
								dstBufferOffset = Mathf.CeilToInt(laserSamplesH * (dstSampleIndexV + 1 - bufferLengthRatio)) - copyLength;
								// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4:F6} bufferLengthRatio{5} srcBufferOffset {6} dstBufferOffset {7}",
								// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, bufferLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
							}
							else if (dataStartAngleH > laserStartAngleH && laserEndAngleH >= dataStartAngleH) // end side
							{
								var dataCopyLengthRatio = Mathf.Abs(laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
								copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * dataCopyLengthRatio);
								srcBufferOffset = srcBufferHorizontalLength * (srcSampleIndexV + 1) - copyLength;
								dstBufferOffset = laserSamplesH * dstSampleIndexV;
								// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataCopyLengthRatio {4:F6} copyLength{5} srcBufferOffset {6} dstBufferOffset {7}",
								// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataCopyLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
							}
							else
							{
								// Debug.LogWarning($"exception case dataAngleH {dataStartAngleH}~{dataEndAngleH} laserAngleH {laserStartAngleH}~{laserEndAngleH}");
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

					_messageQueue.Enqueue(laserScanStamped);
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
