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
using Stopwatch = System.Diagnostics.Stopwatch;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using Unity.Jobs;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private messages.LaserScan _laserScan = null;
		private Thread _laserProcessThread = null;

		private const int BatchSize = 8;
		private const float DEG180 = Mathf.PI * Mathf.Rad2Deg;
		private const float DEG360 = DEG180 * 2;

		private const float HFOV_FOR_2D_LIDAR = 90f;
		private const float HFOV_FOR_3D_LIDAR = 10f;
		private float LaserCameraHFov = 0f;
		private float LaserCameraHFovHalf = 0;
		private float LaserCameraVFov = 0;

		public MathUtil.MinMax scanRange;
		public float rangeResolution = 0;
		public LaserData.Scan horizontal;
		public LaserData.Scan vertical;

		private Transform _lidarLink = null;

		private UnityEngine.Camera _laserCam = null;
		private Material depthMaterial = null;

		private LaserData.AngleResolution _laserAngleResolution;

		private int numberOfLaserCamData = 0;

		private bool _startLaserWork = false;

		private RTHandle _rtHandle = null;
		private ParallelOptions _parallelOptions = null;

		private LaserData.LaserCamData[] _laserCamData;
		private LaserData.LaserDataOutput[] _laserDataOutput;
		private LaserFilter _laserFilter = null;
		private Noise _noise = null;

		public void SetupNoise(in SDF.Noise param)
		{
			if (param != null)
			{
				Debug.Log($"{DeviceName}: Apply noise type:{param.type} mean:{param.mean} stddev:{param.stddev}");
				_noise = new Noise(param);
			}
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_lidarLink = transform.parent;

			var laserSensor = new GameObject("__laser__");
			laserSensor.transform.SetParent(this.transform, false);
			laserSensor.transform.localPosition = Vector3.zero;
			laserSensor.transform.localRotation = Quaternion.identity;
			_laserCam = laserSensor.AddComponent<UnityEngine.Camera>();

			_laserProcessThread = new Thread(() => LaserProcessing());
		}

		protected override void OnStart()
		{
			if (_laserCam != null)
			{
				SetupLaserCamera();

				SetupLaserCameraData();

				_startLaserWork = true;

				StartCoroutine(CaptureLaserCamera());

				_laserProcessThread.Start();
			}
		}


		protected new void OnDestroy()
		{
			_startLaserWork = false;

			if (_laserProcessThread != null && _laserProcessThread.IsAlive)
			{
				_laserProcessThread.Join();
			}

			StopAllCoroutines();
			_rtHandle?.Release();

			base.OnDestroy();
		}

		protected override void InitializeMessages()
		{
			_laserScan = new messages.LaserScan();
			_laserScan.WorldPose = new messages.Pose();
			_laserScan.WorldPose.Position = new messages.Vector3d();
			_laserScan.WorldPose.Orientation = new messages.Quaternion();

			if (vertical.Equals(default(LaserData.Scan)))
			{
				vertical = new LaserData.Scan(1);
			}
		}

		protected override void SetupMessages()
		{
			_laserScan.Frame = DeviceName;
			_laserScan.Count = horizontal.samples;
			_laserScan.AngleMin = horizontal.angle.min * Mathf.Deg2Rad;
			_laserScan.AngleMax = horizontal.angle.max * Mathf.Deg2Rad;
			_laserScan.AngleStep = horizontal.angleStep * Mathf.Deg2Rad;

			_laserScan.RangeMin = scanRange.min;
			_laserScan.RangeMax = scanRange.max;

			_laserScan.VerticalCount = vertical.samples;
			_laserScan.VerticalAngleMin = vertical.angle.min * Mathf.Deg2Rad;
			_laserScan.VerticalAngleMax = vertical.angle.max * Mathf.Deg2Rad;
			_laserScan.VerticalAngleStep = vertical.angleStep * Mathf.Deg2Rad;

			var totalSamples = _laserScan.Count * _laserScan.VerticalCount;

			// Debug.Log(_laserScan.VerticalCount + ", " + _laserScan.VerticalAngleMin + ", " + _laserScan.VerticalAngleMax + ", " + _laserScan.VerticalAngleStep);
			// Debug.Log(_laserScan.Count + " x " + _laserScan.VerticalCount + " = " + totalSamples);
			// Debug.Log($"angle step: deg H:{horizontal.angleStep} V:{vertical.angleStep}, rad H:{_laserScan.AngleStep} V:{_laserScan.VerticalAngleStep}");

			_laserScan.Ranges = new double[totalSamples];
			_laserScan.Intensities = new double[totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, double.NaN);

			_laserAngleResolution = new LaserData.AngleResolution((float)horizontal.angleStep, (float)vertical.angleStep);
			// Debug.Log("H resolution: " + _laserAngleResolution.H + ", V resolution: " + _laserAngleResolution.V);
		}

		private void SetupLaserCamera()
		{
			LaserCameraVFov = (vertical.samples == 1) ? 1 : (Mathf.Max(Mathf.Abs(vertical.angle.min), Mathf.Abs(vertical.angle.max)) * 2);
			LaserCameraHFov = (vertical.samples > 1) ? HFOV_FOR_3D_LIDAR : HFOV_FOR_2D_LIDAR;
			LaserCameraHFovHalf = LaserCameraHFov * 0.5f;

			_laserCam.ResetWorldToCameraMatrix();
			_laserCam.ResetProjectionMatrix();

			_laserCam.allowHDR = true;
			_laserCam.allowMSAA = false;
			_laserCam.allowDynamicResolution = false;
			_laserCam.useOcclusionCulling = true;

			_laserCam.stereoTargetEye = StereoTargetEyeMask.None;

			_laserCam.orthographic = false;
			_laserCam.nearClipPlane = scanRange.min;
			_laserCam.farClipPlane = scanRange.max;
			_laserCam.cullingMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Plane");

			_laserCam.clearFlags = CameraClearFlags.Nothing;
			_laserCam.depthTextureMode = DepthTextureMode.Depth;

			_laserCam.renderingPath = RenderingPath.DeferredShading;

			var renderTextureWidth = Mathf.CeilToInt(LaserCameraHFov / _laserAngleResolution.H);
			var renderTextureHeight = Mathf.CeilToInt(LaserCameraVFov / _laserAngleResolution.V);
			// Debug.Log($"SetupLaserCamera: {LaserCameraHFov} {_laserAngleResolution.H} {LaserCameraVFov} {_laserAngleResolution.V}, {renderTextureWidth} {renderTextureHeight}");

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
				name: "LidarDepthTexture");

			_laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = SensorHelper.MakeProjectionMatrixPerspective(LaserCameraHFov, LaserCameraVFov, _laserCam.nearClipPlane, _laserCam.farClipPlane);
			_laserCam.projectionMatrix = projMatrix;

			var universalLaserCamData = _laserCam.GetUniversalAdditionalCameraData();
			universalLaserCamData.renderShadows = false;
			universalLaserCamData.stopNaN = true;
			universalLaserCamData.dithering = true;
			universalLaserCamData.allowXRRendering = false;
			universalLaserCamData.volumeLayerMask = LayerMask.GetMask("Nothing");
			universalLaserCamData.renderType = CameraRenderType.Base;
			universalLaserCamData.requiresColorOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresDepthOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresColorTexture = false;
			universalLaserCamData.requiresDepthTexture = true;
			universalLaserCamData.cameraStack.Clear();

			var depthShader = Shader.Find("Sensor/DepthRange");
			depthMaterial = new Material(depthShader);

			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			_laserCam.AddCommandBuffer(CameraEvent.AfterEverything, cb);

			cb.ReleaseTemporaryRT(tempTextureId);
			cb.Release();

			// _laserCam.hideFlags |= HideFlags.NotEditable;
			_laserCam.enabled = false;

			if (_noise != null)
			{
				_noise.SetCustomNoiseParameter(GetPluginParameters());
				_noise.SetClampMin(scanRange.min);
				_noise.SetClampMax(scanRange.max);
			}
		}

		private void SetupLaserCameraData()
		{
			var LaserCameraVFovHalf = LaserCameraVFov * 0.5f;
			var LaserCameraRotationAngle = LaserCameraHFov;
			numberOfLaserCamData = Mathf.CeilToInt(DEG360 / LaserCameraRotationAngle);
			var isEven = (numberOfLaserCamData % 2 == 0) ? true : false;

			_parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = numberOfLaserCamData };
			_laserCamData = new LaserData.LaserCamData[numberOfLaserCamData];
			_laserDataOutput = new LaserData.LaserDataOutput[numberOfLaserCamData];

			var targetDepthRT = _laserCam.targetTexture;
			var width = targetDepthRT.width;
			var height = targetDepthRT.height;
			var centerAngleOffset = (horizontal.angle.min < 0) ? (isEven ? -LaserCameraHFovHalf : 0) : LaserCameraHFovHalf;

			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				var centerAngle = LaserCameraRotationAngle * index + centerAngleOffset;
				_laserCamData[index] = new LaserData.LaserCamData(width, height, scanRange, rangeResolution, _laserAngleResolution, centerAngle, LaserCameraHFovHalf, LaserCameraVFovHalf);
			}
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

		private IEnumerator CaptureLaserCamera()
		{
			var lidarSensorWorldPose = new Pose();
			var axisRotation = Vector3.zero;
			var sw = new Stopwatch();

			while (_startLaserWork)
			{
				sw.Restart();

				var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

				lidarSensorWorldPose.position = transform.position;
				lidarSensorWorldPose.rotation = transform.rotation;

				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var laserCamData = _laserCamData[dataIndex];
					axisRotation.y = laserCamData.centerAngle;

					_laserCam.transform.localRotation = Quaternion.Euler(axisRotation);
					_laserCam.enabled = true;

					if (_laserCam.isActiveAndEnabled)
					{
						_laserCam.Render();

						int localDataIndex = dataIndex;
						var localCapturedTime = capturedTime;
						var localSensorWorldPose = lidarSensorWorldPose;

						AsyncGPUReadback.Request(_laserCam.targetTexture, 0, GraphicsFormat.R32_SFloat, (req) => {
							if (req.hasError)
							{
								Debug.LogWarning("Failed to read GPU texture");
							}
							else if (req.done)
							{
								var laserCamData = _laserCamData[localDataIndex];
								laserCamData.depthBuffer = req.GetData<float>();
								laserCamData.Allocate();

								var jobHandle = laserCamData.Schedule(laserCamData.OutputLength(), BatchSize);
								jobHandle.Complete();

								var laserDataOutput = new LaserData.LaserDataOutput(laserCamData.OutputLength());
								laserDataOutput.capturedTime = localCapturedTime;
								laserDataOutput.worldPose = localSensorWorldPose;
								laserCamData.CopyLaserData(ref laserDataOutput.data);

								_laserDataOutput[localDataIndex] = laserDataOutput;

								laserCamData.Deallocate();
							}
						});

						_laserCam.enabled = false;
					}
				}

				sw.Stop();

				var requestingTime = (float)sw.ElapsedMilliseconds + 0.001f;
				yield return new WaitForSeconds(WaitPeriod(requestingTime));
			}
		}

		private void LaserProcessing()
		{
			const int BufferUnitSize = sizeof(double);

			var laserScanStamped = new messages.LaserScanStamped();
			laserScanStamped.Time = new messages.Time();

			var sw = new Stopwatch();

			var laserSamplesH = (int)horizontal.samples;
			var laserStartAngleH = (float)horizontal.angle.min;
			var laserEndAngleH = (float)horizontal.angle.max;
			var laserTotalAngleH = (float)horizontal.angle.range;
			var dividedLaserTotalAngleH = 1f / laserTotalAngleH;

			var laserSamplesV = (int)vertical.samples;
			var laserSamplesVTotal = Mathf.CeilToInt(LaserCameraVFov * vertical.samples / vertical.angle.range);
			var isMaxAngleDominant = Mathf.Abs(vertical.angle.max) > Mathf.Abs(vertical.angle.min);
			var laserSamplesVStart = isMaxAngleDominant ? (laserSamplesVTotal - laserSamplesV) : 0;
			var laserSamplesVEnd = isMaxAngleDominant ? laserSamplesVTotal : laserSamplesV;

			// Debug.Log($"laserSamplesVTotal: {laserSamplesVTotal}, " +
			// 			$"isMaxAngleDominant: {isMaxAngleDominant}, " +
			// 			$"laserSamplesVStart: {laserSamplesVStart}, " +
			// 			$"laserSamplesVEnd: {laserSamplesVEnd} " +
			// 			$"laserScan.Ranges: {_laserScan.Ranges.LongLength}");

			while (_startLaserWork)
			{
				sw.Restart();

				laserScanStamped.Scan = _laserScan;
				var laserScan = laserScanStamped.Scan;

				var sensorWorldPose = _laserDataOutput[0].worldPose;
				laserScan.WorldPose.Position.Set(sensorWorldPose.position);
				laserScan.WorldPose.Orientation.Set(sensorWorldPose.rotation);

				Array.Fill(laserScan.Ranges, double.NaN);

				var capturedTime = _laserDataOutput[0].capturedTime;

				Parallel.For(0, numberOfLaserCamData, _parallelOptions, index =>
				{
					var laserCamData = _laserCamData[index];
					var laserDataOutput = _laserDataOutput[index];
					var srcBuffer = laserDataOutput.data;
					if (srcBuffer == null)
					{
						return;
					}

					var srcBufferHorizontalLength = laserCamData.horizontalBufferLength;
					var dataStartAngleH = laserCamData.StartAngleH;
					var dataEndAngleH = laserCamData.EndAngleH;
					var dividedDataTotalAngleH = 1f / laserCamData.TotalAngleH;

					if (laserStartAngleH < 0 && dataEndAngleH > DEG180)
					{
						dataStartAngleH -= DEG360;
						dataEndAngleH -= DEG360;
					}

					var dstSampleIndexV = 0;
					for (var srcSampleIndexV = laserSamplesVStart; srcSampleIndexV < laserSamplesVEnd; srcSampleIndexV++)
					{
						int srcBufferOffset = 0;
						int dstBufferOffset = 0;
						int copyLength = 0;
						bool doCopy = true;

						if (dataStartAngleH <= laserStartAngleH && laserStartAngleH < dataEndAngleH) // start side
						{
							if (laserEndAngleH >= dataEndAngleH)
							{
								var dataLengthRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
								copyLength = srcBufferHorizontalLength - Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
								srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV;
								dstBufferOffset = laserSamplesH * dstSampleIndexV + (laserSamplesH - copyLength);
								// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4} copyLength{5} srcBufferOffset {6} dstBufferOffset {7}",
								// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
							}
							else
							{
								var dataLengthRatio = (laserEndAngleH - laserStartAngleH) * dividedDataTotalAngleH;
								var startBufferOffsetRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
								copyLength = Mathf.FloorToInt(srcBufferHorizontalLength * dataLengthRatio) - 1;
								srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV + Mathf.CeilToInt(srcBufferHorizontalLength * startBufferOffsetRatio);
								dstBufferOffset = laserSamplesH * dstSampleIndexV;
								// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4} copyLength{5} srcBufferOffset {6} dstBufferOffset {7} startBufferOffsetRatio{8} index{9} srcBufferHorizontalLength{10} srcBuffer.Length{11}" ,
								// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataLengthRatio, copyLength, srcBufferOffset, dstBufferOffset, startBufferOffsetRatio, index, srcBufferHorizontalLength, srcBuffer.Length);
							}
						}
						else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH) // middle
						{
							var dataLengthRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
							copyLength = srcBufferHorizontalLength;
							srcBufferOffset = srcBufferHorizontalLength * srcSampleIndexV;
							dstBufferOffset = Mathf.CeilToInt(laserSamplesH * (dstSampleIndexV + 1 - dataLengthRatio)) - copyLength;
							// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4} copyLength{5} srcBufferOffset {6} dstBufferOffset {7}",
							// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
						}
						else if (dataStartAngleH > laserStartAngleH && laserEndAngleH >= dataStartAngleH) // end side
						{
							var dataLengthRatio = Mathf.Abs(laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
							copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
							srcBufferOffset = srcBufferHorizontalLength * (srcSampleIndexV + 1) - copyLength;
							dstBufferOffset = laserSamplesH * dstSampleIndexV;
							// Debug.LogFormat("dataAngleH {0}~{1} laserAngleH {2}~{3} dataLengthRatio {4} copyLength{5} srcBufferOffset {6} dstBufferOffset {7}",
							// 	dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH, dataLengthRatio, copyLength, srcBufferOffset, dstBufferOffset);
						}
						else
						{
							// Debug.LogFormat("exception case dataAngleH {0}~{1} laserAngleH {2}~{3}", dataStartAngleH, dataEndAngleH, laserStartAngleH, laserEndAngleH);
							doCopy = false;
						}

						if (doCopy && copyLength > 0 && dstBufferOffset >= 0)
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

				laserScanStamped.Time.Set(capturedTime);
				_messageQueue.Enqueue(laserScanStamped);

				sw.Stop();
				Thread.Sleep(WaitPeriodInMilliseconds());
			}
		}

		[SerializeField] private static int _indexForVisualize = 0;
		[SerializeField] private static int _maxCountForVisualize = 3;
		[SerializeField] private static float _hueOffsetForVisualize = 0f;
		[SerializeField] private const float UnitHueOffsetForVisualize = 0.07f;
		[SerializeField] private const float AlphaForVisualize = 0.75f;

		protected override IEnumerator OnVisualize()
		{
			var visualDrawDuration = UpdatePeriod;

			var startAngleH = horizontal.angle.min;
			var startAngleV = vertical.angle.max;
			var endAngleV = vertical.angle.min;
			var angleRangeV = vertical.angle.range;
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			var horizontalSamples = horizontal.samples;
			var rangeMin = scanRange.min;
			var rangeMax = scanRange.max;

			if (_indexForVisualize >= _maxCountForVisualize)
			{
				_indexForVisualize = 0;
				_hueOffsetForVisualize += UnitHueOffsetForVisualize;
			}
			var hue = ((float)_indexForVisualize++ / Mathf.Max(1, _maxCountForVisualize)) + _hueOffsetForVisualize;
			hue = (hue % 1f + 1f) % 1f;
			// Debug.Log($"hue{hue} _indexForVisualize{_indexForVisualize}");
			var baseRayColor = Color.HSVToRGB(hue, 0.9f, 1f);

			var lidarModel = _lidarLink.parent;

			while (true)
			{
				var rayStartBase = transform.position;
				var sensorWorldRotation = transform.rotation;

				var rangeData = GetRangeData();

				if (rangeData != null)
				{
					var localUp = _lidarLink.up;
					var localRight = -_lidarLink.right;
					var localForward = _lidarLink.forward;

					for (var scanIndex = 0; scanIndex < rangeData.Count; scanIndex++)
					{
						var scanIndexH = scanIndex % horizontalSamples;
						var scanIndexV = scanIndex / horizontalSamples;

						var rayAngleH = startAngleH + (_laserAngleResolution.H * scanIndexH);
						var rayAngleV = startAngleV - (_laserAngleResolution.V * scanIndexV);

						var ccwIndex = (int)(rangeData.Count - scanIndex - 1);
						var rayData = (float)rangeData[ccwIndex];

						if (!float.IsNaN(rayData) && rayData <= rangeMax)
						{
							var t = Mathf.InverseLerp(endAngleV, startAngleV, rayAngleV);
							var s = Mathf.Lerp(0.55f, 0.95f, t);
							var rayColor = Color.HSVToRGB(hue, s, 0.95f);
							rayColor.a = AlphaForVisualize;

							var localAngles = Quaternion.AngleAxis(rayAngleH, Vector3.up) * Quaternion.AngleAxis(rayAngleV, -Vector3.right);

							var dir = sensorWorldRotation * localAngles * Vector3.forward;
							dir.Normalize();

							var start = rayStartBase + dir * rangeMin;
							var end = start + dir * (rayData - rangeMin);

							Debug.DrawLine(start, end, rayColor, visualDrawDuration, true);
						}
					}
				}

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
