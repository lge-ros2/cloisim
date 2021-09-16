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

		private const float LaserCameraHFov = 120.0000000000f;
		private const float LaserCameraVFov = 50.0000000000f;
		private const float LaserCameraHFovHalf = LaserCameraHFov * 0.5f;

		public LaserData.MinMax range;

		public LaserData.Scan horizontal;

		public LaserData.Scan vertical;

		private Transform lidarLink = null;
		private Pose lidarSensorInitPose = new Pose();
		private Pose lidarSensorPose = new Pose();

		private UnityEngine.Camera laserCam = null;
		private Material depthMaterial = null;

		private LaserData.AngleResolution laserAngleResolution;

		private int numberOfLaserCamData = 0;

		private bool _startLaserWork = false;
		private float _lastTimeLaserCameraWork = 0;

		private RTHandle _rtHandle = null;

		struct AsyncLaserWork
		{
			public int dataIndex;
			public AsyncGPUReadbackRequest request;

			public AsyncLaserWork(in int dataIndex, in AsyncGPUReadbackRequest request)
			{
				this.dataIndex = dataIndex;
				this.request = request;
			}
		}
		private List<AsyncLaserWork> _readbackList = new List<AsyncLaserWork>();
		private LaserData.DepthCamBuffer[] depthCamBuffers;
		private LaserData.LaserCamData[] laserCamData;
		private LaserData.LaserDataOutput[] laserDataOutput;
		private LaserFilter laserFilter = null;
		public Noise noise = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			lidarLink = transform.parent;

			laserCam = GetComponent<UnityEngine.Camera>();
		}

		protected override void OnStart()
		{
			if (laserCam)
			{
				lidarSensorInitPose.position = transform.localPosition;
				lidarSensorInitPose.rotation = transform.localRotation;

				SetupLaserCamera();

				SetupLaserCameraData();

				_startLaserWork = true;
			}
		}

		void Update()
		{
			if (_startLaserWork)
			{
				LaserCameraWorker();
			}
		}

		protected new void OnDestroy()
		{
			_startLaserWork = false;

			if (_rtHandle != null)
			{
				_rtHandle.Release();
			}

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

			var laserScan = laserScanStamped.Scan;
			laserScan.Frame = DeviceName;
			laserScan.Count = horizontal.samples;
			laserScan.AngleMin = horizontal.angle.min * Mathf.Deg2Rad;
			laserScan.AngleMax = horizontal.angle.max * Mathf.Deg2Rad;
			laserScan.AngleStep = horizontal.angleStep * Mathf.Deg2Rad;

			laserScan.RangeMin = range.min;
			laserScan.RangeMax = range.max;

			if (vertical.Equals(default(LaserData.Scan)))
			{
				vertical = new LaserData.Scan(1);
			}
			laserScan.VerticalCount = vertical.samples;
			laserScan.VerticalAngleMin = vertical.angle.min * Mathf.Deg2Rad;
			laserScan.VerticalAngleMax = vertical.angle.max * Mathf.Deg2Rad;
			laserScan.VerticalAngleStep = vertical.angleStep * Mathf.Deg2Rad;

			var totalSamples = laserScan.Count * laserScan.VerticalCount;
			// Debug.Log(samples + " x " + vertical.samples + " = " + totalSamples);

			laserScan.Ranges = new double[totalSamples];
			laserScan.Intensities = new double[totalSamples];
			Array.Clear(laserScan.Ranges, 0, laserScan.Ranges.Length);
			Array.Clear(laserScan.Intensities, 0, laserScan.Intensities.Length);
			Parallel.ForEach(laserScan.Ranges, item => item = double.NaN);
			Parallel.ForEach(laserScan.Intensities, item => item = double.NaN);

 			laserAngleResolution = new LaserData.AngleResolution((float)horizontal.angleStep, (float)vertical.angleStep);
			// Debug.Log("H resolution: " + laserHAngleResolution + ", V resolution: " + laserVAngleResolution);
		}

		private void SetupLaserCamera()
		{
			laserCam.ResetWorldToCameraMatrix();
			laserCam.ResetProjectionMatrix();

			laserCam.allowHDR = true;
			laserCam.allowMSAA = false;
			laserCam.allowDynamicResolution = false;
			laserCam.useOcclusionCulling = true;

			laserCam.stereoTargetEye = StereoTargetEyeMask.None;

			laserCam.orthographic = false;
			laserCam.nearClipPlane = (float)range.min * Mathf.Sin((90f - LaserCameraHFovHalf) * Mathf.Deg2Rad);
			laserCam.farClipPlane = (float)range.max;
			laserCam.cullingMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Plane");

			laserCam.clearFlags = CameraClearFlags.Nothing;
			laserCam.depthTextureMode = DepthTextureMode.Depth;

			laserCam.renderingPath = RenderingPath.DeferredLighting;

			var renderTextrueWidth = Mathf.CeilToInt(LaserCameraHFov / laserAngleResolution.H);
			var renderTextrueHeight = (laserAngleResolution.V == 1) ? 1 : Mathf.CeilToInt(LaserCameraVFov / laserAngleResolution.V);

			// _rtHandle = RTHandles.Alloc(renderTextrueWidth, renderTextrueHeight, 1,
			// 	DepthBits.None,
			// 	GraphicsFormat.R8G8B8A8_UNorm,
			// 	FilterMode.Bilinear,
			// 	TextureWrapMode.Clamp,
			// 	TextureDimension.Tex2D,
			// 	false, // enableRandomWrite
			// 	false, // useMipMap
			// 	false, // autoGenerateMips
			// 	false, // isShadowMap
			// 	1, // anisoLevel
			// 	0, // mipMapBias
			// 	MSAASamples.None,
			// 	false, // bindTextureMS
			// 	true, // useDynamicScale
			// 	RenderTextureMemoryless.None,
			// 	"LidarDepthTexture");

			var targetDepthRT = new RenderTexture(renderTextrueWidth, renderTextrueHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				name = "LidarDepthTexture",
				dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
				antiAliasing = 1,
				useMipMap = false,
				useDynamicScale = true,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Point,
				enableRandomWrite = false
			};

			_rtHandle = RTHandles.Alloc(targetDepthRT);
			laserCam.targetTexture = _rtHandle.rt;

			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(LaserCameraHFov, LaserCameraVFov, laserCam.nearClipPlane, laserCam.farClipPlane);
			laserCam.projectionMatrix = projMatrix;

			var universalLaserCamData = laserCam.GetUniversalAdditionalCameraData();
			universalLaserCamData.renderShadows = false;
			universalLaserCamData.allowXRRendering = false;
			universalLaserCamData.volumeLayerMask = LayerMask.GetMask("Nothing");
			universalLaserCamData.renderType = CameraRenderType.Base;
			universalLaserCamData.requiresColorOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresDepthOption = CameraOverrideOption.Off;
			universalLaserCamData.requiresColorTexture = false;
			universalLaserCamData.requiresDepthTexture = true;
			universalLaserCamData.cameraStack.Clear();

			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);
			depthMaterial.SetInt("_FlipX", 1); // Store CCW direction for ROS2 sensor data

			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
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
			const float laserCameraRotationAngle = LaserCameraHFov;
			numberOfLaserCamData = Mathf.CeilToInt(DEG360 / laserCameraRotationAngle);

			laserCamData = new LaserData.LaserCamData[numberOfLaserCamData];
			depthCamBuffers = new LaserData.DepthCamBuffer[numberOfLaserCamData];
			laserDataOutput = new LaserData.LaserDataOutput[numberOfLaserCamData];

			var targetDepthRT = laserCam.targetTexture;
			var width = targetDepthRT.width;
			var height = targetDepthRT.height;
			var centerAngleOffset = (horizontal.angle.min < 0) ? 0f : LaserCameraHFovHalf;

			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				depthCamBuffers[index] = new LaserData.DepthCamBuffer(width, height);

				var centerAngle = laserCameraRotationAngle * index + centerAngleOffset;
				var data = new LaserData.LaserCamData(width, height, laserAngleResolution, centerAngle, LaserCameraHFovHalf);
				data.range = range;
				laserCamData[index] = data;
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

		private void LaserCameraWorker()
		{
			if (Time.time - _lastTimeLaserCameraWork >= WaitPeriod(0.0001f))
			{
				// Update lidar sensor pose
				lidarSensorPose.position = lidarLink.position;
				lidarSensorPose.rotation = lidarLink.rotation;

				var axisRotation = Vector3.zero;

				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var data = laserCamData[dataIndex];
					axisRotation.y = data.centerAngle;

					laserCam.transform.localRotation = lidarSensorInitPose.rotation * Quaternion.Euler(axisRotation);

					laserCam.enabled = true;

					if (laserCam.isActiveAndEnabled)
					{
						laserCam.Render();
						var readbackRequest = AsyncGPUReadback.Request(laserCam.targetTexture, 0, TextureFormat.RGBA32, OnCompleteAsyncReadback);
						_readbackList.Add(new AsyncLaserWork(dataIndex, readbackRequest));
					}

					laserCam.enabled = false;
				}

				_lastTimeLaserCameraWork = Time.time;
			}
		}

		protected void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogError("Failed to read GPU texture");
				return;
			}
			else if (request.done)
			{
				var readback = new AsyncLaserWork();
				var dataIndex = -1;

				for (var i = 0; i < _readbackList.Count; i++)
				{
					readback = _readbackList[i];
					if (readback.request.Equals(request))
					{
						dataIndex = readback.dataIndex;
						break;
					}
				}

				if (dataIndex > -1)
				{
					var depthCamBuffer = depthCamBuffers[dataIndex];

					var readbackData = request.GetData<byte>();
					depthCamBuffer.imageBuffer = readbackData;
					depthCamBuffer.Allocate();

					if (depthCamBuffer.depthBuffer.IsCreated)
					{
						var jobHandleDepthCamBuffer = depthCamBuffer.Schedule(depthCamBuffer.Length(), BatchSize);
						jobHandleDepthCamBuffer.Complete();

						var data = laserCamData[dataIndex];
						data.depthBuffer = depthCamBuffer.depthBuffer;
						data.Allocate();

						var jobHandle = data.Schedule(data.OutputLength(), BatchSize);
						jobHandle.Complete();

						laserDataOutput[dataIndex].data = data.GetLaserData();

						if (noise != null)
						{
							noise.Apply<double>(ref laserDataOutput[dataIndex].data);
						}

						data.Deallocate();
					}

					depthCamBuffer.Deallocate();

					readbackData.Dispose();

					_readbackList.Remove(readback);
				}
				else
				{
					Debug.LogWarning("Wrong data Index: " + dataIndex);
				}
			}
		}

		protected override void GenerateMessage()
		{
			var lidarPosition = lidarSensorPose.position + lidarSensorInitPose.position;
			var lidarRotation = lidarSensorPose.rotation * lidarSensorInitPose.rotation;

			var laserScan = laserScanStamped.Scan;

			DeviceHelper.SetVector3d(laserScan.WorldPose.Position, lidarPosition);
			DeviceHelper.SetQuaternion(laserScan.WorldPose.Orientation, lidarRotation);

			var srcBufferOffset = 0;
			var dstBufferOffset = 0;
			var copyLength = 0;
			var doCopy = true;

			const int bufferUnitSize = sizeof(double);
			var laserSamplesH = (int)horizontal.samples;
			var laserStartAngleH = (float)horizontal.angle.min;
			var laserEndAngleH = (float)horizontal.angle.max;
			var laserTotalAngleH = (float)horizontal.angle.range;
			var dividedLaserTotalAngleH = 1f / laserTotalAngleH;

			var laserSamplesV = (int)vertical.samples;
			var laserStartAngleV = (float)vertical.angle.min;
			var laserEndAngleV = (float)vertical.angle.max;
			var laserTotalAngleV = (float)vertical.angle.range;

			Parallel.For(0, numberOfLaserCamData, index =>
			{
				var data = laserCamData[index];
				var srcBuffer = laserDataOutput[index].data;
				var srcBufferHorizontalLength = data.horizontalBufferLength;
				var dataStartAngleH = data.StartAngleH;
				var dataEndAngleH = data.EndAngleH;
				var dividedDataTotalAngleH = 1f / data.TotalAngleH;

				if (srcBuffer != null)
				{
					if (laserStartAngleH < 0 && dataEndAngleH > DEG180)
					{
						dataStartAngleH -= DEG360;
						dataEndAngleH -= DEG360;
					}
					// Debug.LogFormat("index {0}: {1}~{2}, {3}~{4}", dataIndex, laserStartAngleH, laserEndAngleH, dataStartAngleH, dataEndAngleH);

					for (var sampleIndexV = 0; sampleIndexV < laserSamplesV; sampleIndexV++, doCopy = true)
					{
						// start side of laser angle
						if (dataStartAngleH <= laserStartAngleH)
						{
							srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
							var srcLengthratio = Mathf.Abs((dataStartAngleH - laserStartAngleH) * dividedDataTotalAngleH);
							copyLength = srcBufferHorizontalLength - Mathf.FloorToInt(srcBufferHorizontalLength * srcLengthratio);
							dstBufferOffset = (laserSamplesH * (sampleIndexV + 1)) - copyLength;

							if (copyLength < 0 || dstBufferOffset < 0)
							{
								doCopy = false;
							}
						}
						// middle of laser angle
						else if (dataStartAngleH > laserStartAngleH && dataEndAngleH < laserEndAngleH)
						{
							srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
							copyLength = srcBufferHorizontalLength;

							var sampleRatio = (dataStartAngleH - laserStartAngleH) * dividedLaserTotalAngleH;
							dstBufferOffset = (laserSamplesH * (sampleIndexV + 1)) - (Mathf.CeilToInt(laserSamplesH * sampleRatio) + copyLength);

							if (copyLength < 0 || dstBufferOffset < 0)
							{
								doCopy = false;
							}
						}
						// end side of laser angle
						else if (dataEndAngleH >= laserEndAngleH)
						{
							var srcLengthRatio = (laserEndAngleH - dataStartAngleH) * dividedDataTotalAngleH;
							copyLength = Mathf.CeilToInt(srcBufferHorizontalLength * srcLengthRatio);

							srcBufferOffset = (srcBufferHorizontalLength * (sampleIndexV + 1)) - copyLength;
							dstBufferOffset = laserSamplesH * sampleIndexV;

							if (copyLength < 0 || srcBufferOffset < 0)
							{
								doCopy = false;
							}
						}
						else
						{
							Debug.LogWarning("Something wrong data in Laser....");
							doCopy = false;
						}

						if (doCopy)
						{
							try {
								lock (laserScan.Ranges.SyncRoot)
								{
									Buffer.BlockCopy(srcBuffer, srcBufferOffset * bufferUnitSize, laserScan.Ranges, dstBufferOffset * bufferUnitSize, copyLength * bufferUnitSize);
								}
							}
							catch (Exception ex)
							{
								Debug.LogWarning("Error occured with Buffer.BlockCopy : " + ex.Message + ", " + srcBufferOffset + ", " + dstBufferOffset + ", " + copyLength);

							}
						}
					}
				}
			});

			if (laserFilter != null)
			{
				laserFilter.DoFilter(ref laserScan);
			}

			DeviceHelper.SetCurrentTime(laserScanStamped.Time);
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
				var rayStart = lidarLink.position + lidarSensorInitPose.position;
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
							rayColor.g = rayAngleV/(float)angleRangeV;

							var rayRotation = Quaternion.AngleAxis((float)rayAngleH, transform.up) * Quaternion.AngleAxis((float)rayAngleV, transform.forward) * lidarLink.forward;
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