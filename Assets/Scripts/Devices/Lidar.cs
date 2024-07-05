/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */


using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		private messages.LaserScanStamped laserScanStamped = null;

		private const int BatchSize = 8;
		private const float DEG180 = Mathf.PI * Mathf.Rad2Deg;
		private const float DEG360 = DEG180 * 2;

		private const float HFOV_FOR_2D_LIDAR = 120f;
		private const float HFOV_FOR_3D_LIDAR = 10f;
		private float LaserCameraHFov = 0f;
		private float LaserCameraHFovHalf = 0;
		private float LaserCameraVFov = 0;

		public LaserData.MinMax range;
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

		private List<AsyncLaserWork> _asyncWorkList = new List<AsyncLaserWork>();
		private DepthData.CamBuffer[] _depthCamBuffers;
		private LaserData.LaserCamData[] _laserCamData;
		private LaserData.LaserDataOutput[] _laserDataOutput;
		private LaserFilter laserFilter = null;
		public Noise noise = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_lidarLink = transform.parent;

			laserCam = GetComponent<UnityEngine.Camera>();
		}

		protected override void OnStart()
		{
			if (laserCam)
			{
				_lidarSensorInitPose.position = transform.localPosition;
				_lidarSensorInitPose.rotation = transform.localRotation;

				SetupLaserCamera();

				SetupLaserCameraData();

				_startLaserWork = true;
				StartCoroutine(CaptureLaserCamera());
			}
		}

		protected override void OnReset()
		{
			lock (_asyncWorkList)
			{
				_asyncWorkList.Clear();
			}
		}

		protected new void OnDestroy()
		{
			_startLaserWork = false;

			_rtHandle?.Release();

			base.OnDestroy();
		}

		protected override void InitializeMessages()
		{
			laserScanStamped = new messages.LaserScanStamped();
			laserScanStamped.Time = new messages.Time();
			laserScanStamped.Scan = new messages.LaserScan();
			laserScanStamped.Scan.WorldPose = new messages.Pose();
			laserScanStamped.Scan.WorldPose.Position = new messages.Vector3d();
			laserScanStamped.Scan.WorldPose.Orientation = new messages.Quaternion();

			if (vertical.Equals(default(LaserData.Scan)))
			{
				vertical = new LaserData.Scan(1);
			}
		}

		protected override void SetupMessages()
		{
			var laserScan = laserScanStamped.Scan;
			laserScan.Frame = DeviceName;
			laserScan.Count = horizontal.samples;
			laserScan.AngleMin = horizontal.angle.min * Mathf.Deg2Rad;
			laserScan.AngleMax = horizontal.angle.max * Mathf.Deg2Rad;
			laserScan.AngleStep = horizontal.angleStep * Mathf.Deg2Rad;

			laserScan.RangeMin = range.min;
			laserScan.RangeMax = range.max;

			laserScan.VerticalCount = vertical.samples;
			laserScan.VerticalAngleMin = vertical.angle.min * Mathf.Deg2Rad;
			laserScan.VerticalAngleMax = vertical.angle.max * Mathf.Deg2Rad;
			laserScan.VerticalAngleStep = vertical.angleStep * Mathf.Deg2Rad;
			// Debug.Log(laserScan.VerticalCount + ", " + laserScan.VerticalAngleMin + ", " + laserScan.VerticalAngleMax + ", " + laserScan.VerticalAngleStep);

			var totalSamples = laserScan.Count * laserScan.VerticalCount;
			// Debug.Log(samples + " x " + vertical.samples + " = " + totalSamples);

			laserScan.Ranges = new double[totalSamples];
			laserScan.Intensities = new double[totalSamples];
			Array.Clear(laserScan.Ranges, 0, laserScan.Ranges.Length);
			Array.Clear(laserScan.Intensities, 0, laserScan.Intensities.Length);
			Parallel.ForEach(laserScan.Ranges, item => item = double.NaN);
			Parallel.ForEach(laserScan.Intensities, item => item = double.NaN);

			laserAngleResolution = new LaserData.AngleResolution((float)horizontal.angleStep, (float)vertical.angleStep);
			// Debug.Log("H resolution: " + laserAngleResolution.H + ", V resolution: " + laserAngleResolution.V);
		}

		private void SetupLaserCamera()
		{
			LaserCameraVFov = (vertical.samples == 1) ? 1 : (float)vertical.angle.max - (float)vertical.angle.min;
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
			laserCam.nearClipPlane = (float)range.min;
			laserCam.farClipPlane = (float)range.max;
			laserCam.cullingMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Plane");

			laserCam.clearFlags = CameraClearFlags.Nothing;
			laserCam.depthTextureMode = DepthTextureMode.Depth;

			laserCam.renderingPath = RenderingPath.DeferredShading;

			var renderTextrueWidth = Mathf.CeilToInt(LaserCameraHFov / laserAngleResolution.H);
			var renderTextrueHeight = Mathf.CeilToInt(LaserCameraVFov / laserAngleResolution.V);
			// Debug.Log(maxVFov + "," + LaserCameraVFov + "," + renderTextrueWidth + "," + renderTextrueHeight);
			// Debug.Log(LaserCameraVFov + "," + renderTextrueWidth + "," + renderTextrueHeight);

			RTHandles.SetHardwareDynamicResolutionState(true);
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
				useDynamicScale: true,
				memoryless: RenderTextureMemoryless.None,
				name: "LidarDepthTexture");

			laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(LaserCameraHFov, LaserCameraVFov, laserCam.nearClipPlane, laserCam.farClipPlane);
			// Debug.Log("Cam VFOV=" + laserCameraVFov);
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

			if (noise != null)
			{
				noise.SetClampMin(range.min);
				noise.SetClampMax(range.max);
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
				_laserCamData[index] = new LaserData.LaserCamData(width, height, range, laserAngleResolution, centerAngle, LaserCameraHFovHalf, LaserCameraVFovHalf);
			}
		}

		public void SetupLaserAngleFilter(in double filterAngleLower, in double filterAngleUpper, in bool useIntensity = false)
		{
			if (laserFilter == null)
			{
				laserFilter = new LaserFilter(laserScanStamped.Scan, useIntensity);
			}

			laserFilter.SetupAngleFilter(filterAngleLower, filterAngleUpper);
		}

		public void SetupLaserRangeFilter(in double filterRangeMin, in double filterRangeMax, in bool useIntensity = false)
		{
			if (laserFilter == null)
			{
				laserFilter = new LaserFilter(laserScanStamped.Scan, useIntensity);
			}

			laserFilter.SetupRangeFilter(filterRangeMin, filterRangeMax);
		}

		private IEnumerator CaptureLaserCamera()
		{
			while (_startLaserWork)
			{
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

						lock (_asyncWorkList)
						{
							_asyncWorkList.Add(new AsyncLaserWork(dataIndex, readbackRequest, capturedTime));
						}

						laserCam.enabled = false;
						yield return null;
					}
				}

				var processingTime = 0f;
				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					processingTime += _laserDataOutput[dataIndex].processingTime;
				}

				var averageProcessingTime = processingTime / numberOfLaserCamData;
				yield return new WaitForSeconds(UpdatePeriod - averageProcessingTime);
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
				AsyncLaserWork asyncWork;

				lock (_asyncWorkList)
				{
					asyncWork = _asyncWorkList.Find(x => x.request.Equals(request));
				}

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

					_laserDataOutput[dataIndex].data = laserCamData.GetLaserData();
					_laserDataOutput[dataIndex].capturedTime = asyncWork.capturedTime;
					_laserDataOutput[dataIndex].processingTime = (float)DeviceHelper.GlobalClock.SimTime - asyncWork.capturedTime;

					if (noise != null)
					{
						noise.Apply<double>(ref _laserDataOutput[dataIndex].data);
					}

					laserCamData.Deallocate();
				}

				depthCamBuffer.Deallocate();

				lock (_asyncWorkList)
				{
					_asyncWorkList.Remove(asyncWork);
				}
			}
		}

		protected override void GenerateMessage()
		{
			var lidarPosition = _lidarSensorPose.position + _lidarSensorInitPose.position;
			var lidarRotation = _lidarSensorPose.rotation * _lidarSensorInitPose.rotation;

			var laserScan = laserScanStamped.Scan;

			DeviceHelper.SetVector3d(laserScan.WorldPose.Position, lidarPosition);
			DeviceHelper.SetQuaternion(laserScan.WorldPose.Orientation, lidarRotation);

			const int BufferUnitSize = sizeof(double);
			var laserSamplesH = (int)horizontal.samples;
			var laserStartAngleH = (float)horizontal.angle.min;
			var laserEndAngleH = (float)horizontal.angle.max;
			var laserTotalAngleH = (float)horizontal.angle.range;
			var dividedLaserTotalAngleH = 1f / laserTotalAngleH;

			var laserSamplesV = (int)vertical.samples;
			var laserStartAngleV = (float)vertical.angle.min;
			var laserEndAngleV = (float)vertical.angle.max;
			var laserTotalAngleV = (float)vertical.angle.range;

			var capturedTimeSum = 0f;

			Parallel.For(0, numberOfLaserCamData, _parallelOptions, index =>
			{
				var laserCamData = _laserCamData[index];
				var srcBuffer = _laserDataOutput[index].data;
				var srcBufferHorizontalLength = laserCamData.horizontalBufferLength;
				var dataStartAngleH = laserCamData.StartAngleH;
				var dataEndAngleH = laserCamData.EndAngleH;
				var dividedDataTotalAngleH = 1f / laserCamData.TotalAngleH;

				var srcBufferOffset = 0;
				var dstBufferOffset = 0;
				var copyLength = 0;
				var dataLengthRatio = 0f;
				var doCopy = true;

				if (srcBuffer == null)
				{
					return;
				}

				capturedTimeSum += _laserDataOutput[index].capturedTime;

				if (laserStartAngleH < 0 && dataEndAngleH > DEG180)
				{
					dataStartAngleH -= DEG360;
					dataEndAngleH -= DEG360;
				}
				// Debug.LogWarning(index + " capturedTime=" + _laserDataOutput[index].capturedTime.ToString("F8")
				// 				+ ", processingTime=" + _laserDataOutput[index].processingTime.ToString("F8"));
				// Debug.LogFormat("index {0}: {1}~{2}, {3}~{4}", dataIndex, laserStartAngleH, laserEndAngleH, dataStartAngleH, dataEndAngleH);

				for (var sampleIndexV = 0; sampleIndexV < laserSamplesV; sampleIndexV++, doCopy = true)
				{
					if (dataStartAngleH <= laserStartAngleH) // start side of laser angle
					{
						dataLengthRatio = (laserStartAngleH - dataStartAngleH) * dividedDataTotalAngleH;
						copyLength = srcBufferHorizontalLength - Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
						srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
						dstBufferOffset = laserSamplesH * (sampleIndexV + 1) - copyLength;
					}
					else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH) // middle of laser angle
					{
						dataLengthRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
						copyLength = srcBufferHorizontalLength;
						srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
						dstBufferOffset = Mathf.CeilToInt(laserSamplesH * ((float)(sampleIndexV + 1) - dataLengthRatio)) - copyLength;
					}
					else if (dataEndAngleH >= laserEndAngleH) // end side of laser angle
					{
						dataLengthRatio = (laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
						copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * dataLengthRatio);
						srcBufferOffset = srcBufferHorizontalLength * (sampleIndexV + 1) - copyLength;
						dstBufferOffset = laserSamplesH * sampleIndexV + 1;
					}
					else
					{
						Debug.LogWarning("Something wrong data copy type in Generating Laser Data....");
						doCopy = false;
					}

					if (doCopy && copyLength >= 0 && dstBufferOffset >= 0)
					{
						try
						{
							lock (laserScan.Ranges.SyncRoot)
							{
								Buffer.BlockCopy(srcBuffer, srcBufferOffset * BufferUnitSize, laserScan.Ranges, dstBufferOffset * BufferUnitSize, copyLength * BufferUnitSize);
							}
						}
						catch (Exception ex)
						{
							var copyType = -1;
							if (dataStartAngleH <= laserStartAngleH)
								copyType = 0;
							else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH)
								copyType = 1;
							else if (dataEndAngleH >= laserEndAngleH)
								copyType = 2;

							Debug.LogWarningFormat("Error occured with Buffer.BlockCopy: {0}, Type: {1} Offset: src({2}) dst({3}) Len: src({4}) dst({5}) copy_size({6})",
								ex.Message, copyType, srcBufferOffset, dstBufferOffset, srcBuffer.Length, laserScan.Ranges.Length, copyLength);
						}
					}
					else
					{
						// Debug.LogWarning("wrong data index "+ copyLength + ", "  + srcBufferOffset + ", "  + dataCopyType);
					}
				}
			});

			if (laserFilter != null)
			{
				laserFilter.DoFilter(ref laserScan);
			}

			capturedTimeSum += (float)DeviceHelper.GlobalClock.SimTime;
			var capturedTime = capturedTimeSum / (numberOfLaserCamData + 1);
			DeviceHelper.SetTime(laserScanStamped.Time, capturedTime);

			PushDeviceMessage<messages.LaserScanStamped>(laserScanStamped);
		}

		protected override IEnumerator OnVisualize()
		{
			var visualDrawDuration = UpdatePeriod * 2.01f;

			var startAngleH = (float)horizontal.angle.min;
			var startAngleV = (float)vertical.angle.min;
			var angleRangeV = vertical.angle.range;
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			var horizontalSamples = horizontal.samples;
			var rangeMin = (float)range.min;
			var rangeMax = (float)range.max;

			var rayColor = Color.red;

			while (true)
			{
				var lidarModel = _lidarLink.parent;
				var rayStart = _lidarLink.position + lidarModel.rotation * _lidarSensorInitPose.position;
				var rangeData = GetRangeData();

				if (rangeData != null)
				{
					for (var scanIndex = 0; scanIndex < rangeData.Length; scanIndex++)
					{
						var scanIndexH = scanIndex % horizontalSamples;
						var scanIndexV = scanIndex / horizontalSamples;

						var rayAngleH = ((laserAngleResolution.H * scanIndexH)) + startAngleH;
						var rayAngleV = ((laserAngleResolution.V * scanIndexV)) + startAngleV;

						var ccwIndex = (uint)(rangeData.Length - scanIndex - 1);
						var rayData = (float)rangeData[ccwIndex];

						if (rayData != float.NaN && rayData <= rangeMax)
						{
							rayColor.g = rayAngleV / (float)angleRangeV;

							var rayRotation = Quaternion.AngleAxis((float)rayAngleH, transform.up) * Quaternion.AngleAxis((float)rayAngleV, -transform.forward) * _lidarLink.forward;
							var rayDirection = rayRotation * (rayData);
							Debug.DrawRay(rayStart, rayDirection, rayColor, visualDrawDuration, true);
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
				return laserScanStamped.Scan.Ranges;
			}
			catch
			{
				return null;
			}
		}
	}
}
