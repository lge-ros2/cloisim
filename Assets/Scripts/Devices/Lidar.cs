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
	[RequireComponent(typeof(UnityEngine.Camera))]
	public partial class Lidar : Device
	{
		private messages.LaserScan _laserScan = null;
		protected ConcurrentQueue<messages.LaserScanStamped> _messageQueue = new ConcurrentQueue<messages.LaserScanStamped>();
		private Thread _laserProcessThread = null;

		private const int BatchSize = 8;
		private const float DEG180 = Mathf.PI * Mathf.Rad2Deg;
		private const float DEG360 = DEG180 * 2;

		private const float HFOV_FOR_2D_LIDAR = 120f;
		private const float HFOV_FOR_3D_LIDAR = 10f;
		private float LaserCameraHFov = 0f;
		private float LaserCameraHFovHalf = 0;
		private float LaserCameraVFov = 0;

		public MathUtil.MinMax scanRange;
		public LaserData.Scan horizontal;
		public LaserData.Scan vertical;

		private Transform _lidarLink = null;
		private Pose _lidarSensorInitPose = new Pose();
		private Pose _lidarSensorPose = new Pose();

		private UnityEngine.Camera laserCam = null;
		private Material depthMaterial = null;

		private LaserData.AngleResolution laserAngleResolution;

		private int numberOfLaserCamData = 0;

		private bool _startLaserWork = false;

		private RTHandle _rtHandle = null;
		private ParallelOptions _parallelOptions = null;

		private ConcurrentDictionary<int, AsyncWork.Laser> _asyncWorkList = new ConcurrentDictionary<int, AsyncWork.Laser>();
		private DepthData.CamBuffer[] _depthCamBuffers;
		private LaserData.LaserCamData[] _laserCamData;
		private LaserData.LaserDataOutput[] _laserDataOutput;
		private LaserFilter _laserFilter = null;
		private Noise _noise = null;

		public double[] _rangesForVisualize = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_lidarLink = transform.parent;

			laserCam = GetComponent<UnityEngine.Camera>();

			_laserProcessThread = new Thread(() => LaserProcessing());
		}

		protected override void OnStart()
		{
			if (laserCam != null)
			{
				_lidarSensorInitPose.position = transform.localPosition;
				_lidarSensorInitPose.rotation = transform.localRotation;

				SetupLaserCamera();

				SetupLaserCameraData();

				_startLaserWork = true;

				StartCoroutine(CaptureLaserCamera());

				_laserProcessThread.Start();
			}
		}

		protected override void OnReset()
		{
			_messageQueue.Clear();
			_asyncWorkList.Clear();
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

			OnReset();
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

			// Debug.Log(laserScan.VerticalCount + ", " + laserScan.VerticalAngleMin + ", " + laserScan.VerticalAngleMax + ", " + laserScan.VerticalAngleStep);
			// Debug.Log(samples + " x " + vertical.samples + " = " + totalSamples);

			_laserScan.Ranges = new double[totalSamples];
			_laserScan.Intensities = new double[totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, double.NaN);

			_rangesForVisualize = new double[totalSamples];

			laserAngleResolution = new LaserData.AngleResolution((float)horizontal.angleStep, (float)vertical.angleStep);
			// Debug.Log("H resolution: " + laserAngleResolution.H + ", V resolution: " + laserAngleResolution.V);
		}

		private void SetupLaserCamera()
		{
			LaserCameraVFov = (vertical.samples == 1) ? 1 : vertical.angle.range;
			LaserCameraHFov = (vertical.samples > 1) ? HFOV_FOR_3D_LIDAR : HFOV_FOR_2D_LIDAR;
			LaserCameraHFovHalf = LaserCameraHFov * 0.5f;

			laserCam.ResetWorldToCameraMatrix();
			laserCam.ResetProjectionMatrix();

			laserCam.allowHDR = true;
			laserCam.allowMSAA = false;
			laserCam.allowDynamicResolution = false;
			laserCam.useOcclusionCulling = true;

			laserCam.stereoTargetEye = StereoTargetEyeMask.None;

			laserCam.orthographic = false;
			laserCam.nearClipPlane = scanRange.min;
			laserCam.farClipPlane = scanRange.max;
			laserCam.cullingMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Plane");

			laserCam.clearFlags = CameraClearFlags.Nothing;
			laserCam.depthTextureMode = DepthTextureMode.Depth;

			laserCam.renderingPath = RenderingPath.DeferredShading;

			var renderTextrueWidth = Mathf.CeilToInt(LaserCameraHFov / laserAngleResolution.H);
			var renderTextrueHeight = Mathf.CeilToInt(LaserCameraVFov / laserAngleResolution.V);
			// Debug.Log("SetupLaserCamera: " + LaserCameraVFov + ","  + laserAngleResolution.V + "," + renderTextrueWidth + "," + renderTextrueHeight);

			RTHandles.SetHardwareDynamicResolutionState(false);
			_rtHandle?.Release();
			_rtHandle = RTHandles.Alloc(
				width: renderTextrueWidth,
				height: renderTextrueHeight,
				slices: 1,
				depthBufferBits: DepthBits.None,
				colorFormat: GraphicsFormat.R8G8B8A8_UNorm,
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

			laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = SensorHelper.MakeProjectionMatrixPerspective(LaserCameraHFov, LaserCameraVFov, laserCam.nearClipPlane, laserCam.farClipPlane);
			laserCam.projectionMatrix = projMatrix;

			var universalLaserCamData = laserCam.GetUniversalAdditionalCameraData();
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

			var depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(depthShader);
			depthMaterial.SetInt("_FlipX", 1); // Store CCW direction for ROS2 sensor data

			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			laserCam.AddCommandBuffer(CameraEvent.AfterEverything, cb);

			cb.ReleaseTemporaryRT(tempTextureId);
			cb.Release();

			// laserCam.hideFlags |= HideFlags.NotEditable;
			laserCam.enabled = false;

			if (_noise != null)
			{
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
			_depthCamBuffers = new DepthData.CamBuffer[numberOfLaserCamData];
			_laserCamData = new LaserData.LaserCamData[numberOfLaserCamData];
			_laserDataOutput = new LaserData.LaserDataOutput[numberOfLaserCamData];

			var targetDepthRT = laserCam.targetTexture;
			var width = targetDepthRT.width;
			var height = targetDepthRT.height;
			var centerAngleOffset = (horizontal.angle.min < 0) ? (isEven ? -LaserCameraHFovHalf : 0) : LaserCameraHFovHalf;

			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				_depthCamBuffers[index] = new DepthData.CamBuffer(width, height);

				var centerAngle = LaserCameraRotationAngle * index + centerAngleOffset;
				_laserCamData[index] = new LaserData.LaserCamData(width, height, scanRange, laserAngleResolution, centerAngle, LaserCameraHFovHalf, LaserCameraVFovHalf);
			}
		}

		public void SetupNoise(in SDF.Noise param)
		{
			_noise = new SensorDevices.Noise(param, "lidar");
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
			var sw = new Stopwatch();
			while (_startLaserWork)
			{
				sw.Restart();

				// Update lidar sensor pose
				_lidarSensorPose.position = _lidarLink.position;
				_lidarSensorPose.rotation = _lidarLink.rotation;

				var axisRotation = Vector3.zero;

				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var laserCamData = _laserCamData[dataIndex];
					axisRotation.y = laserCamData.centerAngle;

					laserCam.transform.localRotation = _lidarSensorInitPose.rotation * Quaternion.Euler(axisRotation);
					laserCam.enabled = true;

					if (laserCam.isActiveAndEnabled)
					{
						laserCam.Render();
						var capturedTime = (float)DeviceHelper.GetGlobalClock().SimTime;
						var readbackRequest = AsyncGPUReadback.Request(laserCam.targetTexture, 0, GraphicsFormat.R8G8B8A8_UNorm, OnCompleteAsyncReadback);

						_asyncWorkList.TryAdd(readbackRequest.GetHashCode(), new AsyncWork.Laser(dataIndex, readbackRequest, capturedTime));

						laserCam.enabled = false;
					}
				}

				sw.Stop();

				var requestingTime = (float)sw.ElapsedMilliseconds * 0.001f;
				yield return new WaitForSeconds(WaitPeriod(requestingTime));
			}
		}

		protected void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogWarning("Failed to read GPU texture");
			}
			else if (request.done)
			{
				if (_asyncWorkList.Remove(request.GetHashCode(), out var asyncWork))
				{
					var dataIndex = asyncWork.dataIndex;
					var depthCamBuffer = _depthCamBuffers[dataIndex];

					depthCamBuffer.Allocate();
					depthCamBuffer.raw = request.GetData<byte>();

					if (depthCamBuffer.depth.IsCreated)
					{
						var jobHandleDepthCamBuffer = depthCamBuffer.Schedule(depthCamBuffer.Length(), BatchSize);
						jobHandleDepthCamBuffer.Complete();

						var laserCamData = _laserCamData[dataIndex];
						laserCamData.depthBuffer = depthCamBuffer.depth;
						laserCamData.Allocate();

						var jobHandle = laserCamData.Schedule(laserCamData.OutputLength(), BatchSize);
						jobHandle.Complete();

						var laserDataOutput = new LaserData.LaserDataOutput();
						laserDataOutput.data = laserCamData.GetLaserData();
						laserDataOutput.capturedTime = asyncWork.capturedTime;
						laserDataOutput.processingTime = (float)DeviceHelper.GlobalClock.SimTime - asyncWork.capturedTime;

						_laserDataOutput[dataIndex] = laserDataOutput;

						laserCamData.Deallocate();
					}

					depthCamBuffer.Deallocate();
				}
			}
		}

		private void LaserProcessing()
		{
			const int BufferUnitSize = sizeof(double);

			var laserScanStamped = new messages.LaserScanStamped();
			laserScanStamped.Time = new messages.Time();

			var sw = new Stopwatch();
			while (_startLaserWork)
			{
				sw.Restart();

				var lidarPosition = _lidarSensorInitPose.position + _lidarSensorPose.position;
				var lidarRotation = _lidarSensorInitPose.rotation * _lidarSensorPose.rotation;

				laserScanStamped.Scan = _laserScan;
				var laserScan = laserScanStamped.Scan;

				laserScan.WorldPose.Position.Set(lidarPosition);
				laserScan.WorldPose.Orientation.Set(lidarRotation);

				var laserSamplesH = (int)horizontal.samples;
				var laserStartAngleH = (float)horizontal.angle.min;
				var laserEndAngleH = (float)horizontal.angle.max;
				var laserTotalAngleH = (float)horizontal.angle.range;
				var dividedLaserTotalAngleH = 1f / laserTotalAngleH;

				var laserSamplesV = (int)vertical.samples;
				var laserStartAngleV = (float)vertical.angle.min;
				var laserEndAngleV = (float)vertical.angle.max;
				var laserTotalAngleV = (float)vertical.angle.range;
				var dividedLaserTotalAngleV = 1f / laserTotalAngleV;

				Array.Fill(laserScan.Ranges, double.NaN);

				var capturedTime = 0f;
				var processingTimeSum = 0f;

				Parallel.For(0, numberOfLaserCamData, _parallelOptions, index =>
				{
					var laserCamData = _laserCamData[index];
					var srcBuffer = _laserDataOutput[index].data;
					if (srcBuffer == null)
					{
						return;
					}

					var srcBufferHorizontalLength = laserCamData.horizontalBufferLength;
					var dataStartAngleH = laserCamData.StartAngleH;
					var dataEndAngleH = laserCamData.EndAngleH;
					var dividedDataTotalAngleH = 1f / laserCamData.TotalAngleH;

					if (_laserDataOutput[index].capturedTime > capturedTime)
						capturedTime = _laserDataOutput[index].capturedTime;

					processingTimeSum += _laserDataOutput[index].processingTime;

					if (laserStartAngleH < 0 && dataEndAngleH > DEG180)
					{
						dataStartAngleH -= DEG360;
						dataEndAngleH -= DEG360;
					}

					for (var sampleIndexV = 0; sampleIndexV < laserSamplesV; sampleIndexV++)
					{
						int srcBufferOffset = 0;
						int dstBufferOffset = 0;
						int copyLength = 0;
						bool doCopy = true;

						if (dataStartAngleH <= laserStartAngleH) // start side
						{
							var dataLengthRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
							copyLength = srcBufferHorizontalLength - Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
							srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
							dstBufferOffset = laserSamplesH * sampleIndexV + (laserSamplesH - copyLength);
						}
						else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH) // middle
						{
							var dataLengthRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
							copyLength = srcBufferHorizontalLength;
							srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
							dstBufferOffset = Mathf.CeilToInt(laserSamplesH * (sampleIndexV + 1 - dataLengthRatio)) - copyLength;
						}
						else if (dataEndAngleH >= laserEndAngleH) // end side
						{
							var dataLengthRatio = (laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
							copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
							srcBufferOffset = srcBufferHorizontalLength * (sampleIndexV + 1) - copyLength;
							dstBufferOffset = laserSamplesH * sampleIndexV;
						}
						else
						{
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
								Debug.LogWarning($"Buffer copy error: {ex.Message} idx={index} V={sampleIndexV}");
							}
						}
					}
				});

				if (_noise != null)
				{
					var ranges = laserScan.Ranges;
					_noise.Apply<double>(ref ranges);
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

		protected override void GenerateMessage()
		{
			var count = _messageQueue.Count;
			while (_messageQueue.TryDequeue(out var msg))
			{
				_rangesForVisualize = msg.Scan.Ranges;
				PushDeviceMessage<messages.LaserScanStamped>(msg);
				Thread.Sleep(WaitPeriodInMilliseconds() / count);
			}
		}

		protected override IEnumerator OnVisualize()
		{
			var visualDrawDuration = UpdatePeriod * 2.01f;

			var startAngleH = (float)horizontal.angle.min;
			var startAngleV = (float)vertical.angle.min;
			var angleRangeV = vertical.angle.range;
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			var horizontalSamples = horizontal.samples;
			var rangeMin = (float)scanRange.min;
			var rangeMax = (float)scanRange.max;

			var rayColor = Color.red;

			while (true)
			{
				var lidarModel = _lidarLink.parent;
				var rayStart = _lidarLink.position + lidarModel.rotation * _lidarSensorInitPose.position;
				var rangeData = GetRangeData();

				if (rangeData != null)
				{
					var localUp = _lidarLink.up;
					var localRight = _lidarLink.right;
					var localForward = _lidarLink.forward;

					for (var scanIndex = 0; scanIndex < rangeData.Length; scanIndex++)
					{
						var scanIndexH = scanIndex % horizontalSamples;
						var scanIndexV = scanIndex / horizontalSamples;

						var rayAngleH = (laserAngleResolution.H * scanIndexH) + startAngleH;
						var rayAngleV = (laserAngleResolution.V * scanIndexV) + startAngleV;

						var ccwIndex = (uint)(rangeData.Length - scanIndex - 1);
						var rayData = (float)rangeData[ccwIndex];

						if (!float.IsNaN(rayData) && rayData <= rangeMax)
						{
							rayColor.g = rayAngleV / (float)angleRangeV;

							var rayRotation = Quaternion.AngleAxis(rayAngleH, localUp) * Quaternion.AngleAxis(rayAngleV, localRight);
							var rayOffsetStart = rayStart + (rayRotation * localForward * rangeMin);
							var rayDirection = rayRotation * localForward * rayData;

							Debug.DrawRay(rayOffsetStart, rayDirection, rayColor, visualDrawDuration, true);
						}
					}
				}

				yield return waitForSeconds;
			}
		}

		public double[] GetRangeData()
		{
			try
			{
				return _rangesForVisualize;
			}
			catch
			{
				return null;
			}
		}
	}
}
