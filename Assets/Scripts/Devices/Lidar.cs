/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Jobs;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private messages.LaserScanStamped laserScanStamped = null;

		readonly public struct MinMax
		{
			public readonly double min;
			public readonly double max;
			public readonly double range;

			public MinMax(in double min = 0, in double max = 0)
			{
				this.min = min;
				this.max = max;
				this.range = max - min;
			}
		}

		readonly public struct Scan
		{
			public readonly uint samples;
			public readonly double resolution;
			public readonly MinMax angle; // degree
			public readonly double angleStep;

			public Scan(in uint samples, in double angleMinRad, in double angleMaxRad, in double resolution = 1)
			{
				this.samples = samples;
				this.resolution = resolution;
				this.angle = new MinMax(angleMinRad * Mathf.Rad2Deg, angleMaxRad * Mathf.Rad2Deg);
				this.angleStep = (angle.max - angle.min) / (resolution * samples);
			}
			public Scan(in uint samples)
			{
				this.samples = samples;
				this.resolution = 1;
				this.angle = new MinMax();
				this.angleStep = 1;
			}
		}

		readonly public struct AngleResolution
		{
			public readonly float H; // degree
			public readonly float V; // degree

			public AngleResolution(in float angleResolutionH = 0, in float angleResolutionV = 0)
			{
				this.H = angleResolutionH;
				this.V = angleResolutionV;
			}
		}

		private const float LaserCameraHFov = 120.0000000000f;
		private const float LaserCameraVFov = 50.0000000000f;


		public MinMax range;

		public Scan horizontal;

		public Scan vertical;

		private Transform lidarLink = null;
		private Pose lidarSensorInitPose = new Pose();

		private UnityEngine.Camera laserCam = null;
		private Material depthMaterial = null;

		private AngleResolution laserAngleResolution;

		private int numberOfLaserCamData = 0;

		private DepthCamBuffer[] depthCamBuffers;
		private LaserCamData[] laserCamData;


		[ColorUsage(true)]
		public Color rayColor = new Color(1, 0.1f, 0.1f, 0.15f);

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			lidarLink = transform.parent;

			laserCam = gameObject.AddComponent<UnityEngine.Camera>();

			waitingPeriodRatio = 0.80f;
		}

		protected override void OnStart()
		{
			if (laserCam)
			{
				lidarSensorInitPose.position = transform.localPosition;
				lidarSensorInitPose.rotation = transform.localRotation;

				DoParseFilter();

				SetupLaserCamera();

				SetupLaserCameraData();

				StartCoroutine(LaserCameraWorker());
			}
		}

		private void OnDestroy()
		{
			// Debug.LogWarning("Destroy");
			// Important!! Native arrays must be disposed manually.
			for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
			{
				var data = laserCamData[dataIndex];
				data.Deallocate();

				var depthBuffer = depthCamBuffers[dataIndex];
				depthBuffer.Deallocate();
			}
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
			laserScan.Frame = deviceName;
			laserScan.Count = horizontal.samples;
			laserScan.AngleMin = horizontal.angle.min * Mathf.Deg2Rad;
			laserScan.AngleMax = horizontal.angle.max * Mathf.Deg2Rad;
			laserScan.AngleStep = horizontal.angleStep * Mathf.Deg2Rad;

			laserScan.RangeMin = range.min;
			laserScan.RangeMax = range.max;

			if (vertical.Equals(default(Scan)))
			{
				vertical = new Scan(1);
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
			for (var i = 0; i < totalSamples; i++)
			{
				laserScan.Ranges[i] = double.NaN;
				laserScan.Intensities[i] = double.NaN;
			}

 			laserAngleResolution = new AngleResolution((float)horizontal.angleStep, (float)vertical.angleStep);
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
			laserCam.nearClipPlane = (float)range.min;
			laserCam.farClipPlane = (float)range.max;
			laserCam.cullingMask = LayerMask.GetMask("Default") | LayerMask.GetMask("Plane");

			laserCam.clearFlags = CameraClearFlags.Color;
			laserCam.depthTextureMode = DepthTextureMode.Depth;

			laserCam.renderingPath = RenderingPath.DeferredLighting;

			var renderTextrueWidth = Mathf.CeilToInt(LaserCameraHFov / laserAngleResolution.H);
			var renderTextrueHeight = (laserAngleResolution.V == 1) ? 1 : Mathf.CeilToInt(LaserCameraVFov / laserAngleResolution.V);
			var targetDepthRT = new RenderTexture(renderTextrueWidth, renderTextrueHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				name = "LidarDepthTexture",
			};

			laserCam.targetTexture = targetDepthRT;

			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(LaserCameraHFov, LaserCameraVFov, (float)range.min, (float)range.max);
			laserCam.projectionMatrix = projMatrix;

			var universalLaserCamData = laserCam.GetUniversalAdditionalCameraData();
			universalLaserCamData.requiresColorTexture = false;
			universalLaserCamData.requiresDepthTexture = true;
			universalLaserCamData.renderShadows = false;

			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);
			// Store CCW direction for ROS2 sensor data
			depthMaterial.SetInt("_FlipX", 1);
			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			laserCam.AddCommandBuffer(CameraEvent.AfterEverything, cb);

			cb.ReleaseTemporaryRT(tempTextureId);
			cb.Release();

			laserCam.enabled = false;
			// laserCam.hideFlags |= HideFlags.NotEditable;
		}

		private void SetupLaserCameraData()
		{
			const float laserCameraRotationAngle = LaserCameraHFov;
			numberOfLaserCamData = Mathf.CeilToInt(360 / laserCameraRotationAngle);

			laserCamData = new LaserCamData[numberOfLaserCamData];
			depthCamBuffers = new DepthCamBuffer[numberOfLaserCamData];

			var targetDepthRT = laserCam.targetTexture;
			var width = targetDepthRT.width;
			var height = targetDepthRT.height;
			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				var depthCamBuffer = new DepthCamBuffer(width, height);
				depthCamBuffers[index] = depthCamBuffer;

				var data = new LaserCamData(width, height, laserAngleResolution);
				data.SetMaxHorizontalHalfAngle(LaserCameraHFov * 0.5f);
				data.centerAngle = laserCameraRotationAngle * index;
				data.rangeMax = (float)range.max;
				laserCamData[index] = data;
			}
		}

		private IEnumerator LaserCameraWorker()
		{
			const int batchSize = 64;
			var axisRotation = Vector3.zero;
			var readbacks = new AsyncGPUReadbackRequest[numberOfLaserCamData];
			var sw = new Stopwatch();

			while (true)
			{
				sw.Restart();

				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var data = laserCamData[dataIndex];
					axisRotation.y = data.centerAngle;

					laserCam.transform.localRotation = lidarSensorInitPose.rotation * Quaternion.Euler(axisRotation);

					laserCam.enabled = true;

					if (laserCam.isActiveAndEnabled)
					{
						laserCam.Render();
						readbacks[dataIndex] = AsyncGPUReadback.Request(laserCam.targetTexture, 0, TextureFormat.RGBA32);
					}

					laserCam.enabled = false;
				}

				yield return null;

				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var readback = readbacks[dataIndex];
					readback.WaitForCompletion();

					if (readback.hasError)
					{
						Debug.LogError("Failed to read GPU texture, dataIndex: " + dataIndex);
						continue;
					}
					// Debug.Assert(readback.done);

					if (readback.done)
					{
						var depthCamBuffer = depthCamBuffers[dataIndex];
						depthCamBuffer.imageBuffer = readback.GetData<byte>();

						var jobHandleDepthCamBuffer = depthCamBuffer.Schedule(depthCamBuffer.Length(), batchSize);
						jobHandleDepthCamBuffer.Complete();

						var data = laserCamData[dataIndex];
						data.depthBuffer = depthCamBuffer.depthBuffer;

						var jobHandle = data.Schedule(data.OutputLength(), batchSize);
						jobHandle.Complete();
					}
					else
					{
						Debug.LogWarning("AsyncGPUReadBackback Request was failed, dataIndex: " + dataIndex);
					}
				}

				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var lidarPosition = lidarLink.position + lidarSensorInitPose.position;
			var lidarRotation = lidarLink.rotation * lidarSensorInitPose.rotation;

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

			var laserSamplesV = (int)vertical.samples;
			var laserStartAngleV = (float)vertical.angle.min;
			var laserEndAngleV = (float)vertical.angle.max;
			var laserTotalAngleV = (float)vertical.angle.range;

			for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
			{
				var data = laserCamData[dataIndex];
				var srcBuffer = data.GetOutputs();
				var srcBufferHorizontalLength = data.horizontalBufferLength;
				var dataStartAngle = data.StartAngleH;
				var dataEndAngle = data.EndAngleH;
				var dataTotalAngle = data.TotalAngleH;

				for (var sampleIndexV = 0; sampleIndexV < laserSamplesV; sampleIndexV++, doCopy = true)
				{
					if (dataEndAngle > 180)
					{
						dataStartAngle -= 360;
						dataEndAngle -= 360;
					}

					// start side of laser angle
					if (dataStartAngle < laserStartAngleH)
					{
						srcBufferOffset = srcBufferHorizontalLength * sampleIndexV;
						var srcLengthratio = Mathf.Abs((dataStartAngle - laserStartAngleH) / dataTotalAngle);
						copyLength = srcBufferHorizontalLength - Mathf.FloorToInt(srcBufferHorizontalLength * srcLengthratio);
						dstBufferOffset = (laserSamplesH * (sampleIndexV + 1)) - copyLength;

						if (copyLength < 0 || dstBufferOffset < 0)
						{
							doCopy = false;
						}
					}
					// middle of laser angle
					else if (dataStartAngle >= laserStartAngleH && dataEndAngle < laserEndAngleH)
					{
						srcBufferOffset = srcBufferHorizontalLength * sampleIndexV; ;
						copyLength = srcBufferHorizontalLength;
						dstBufferOffset = (laserSamplesH * (sampleIndexV + 1)) - (Mathf.CeilToInt(laserSamplesH * ((dataStartAngle - laserStartAngleH) / laserTotalAngleH)) + copyLength);

						if (copyLength < 0 || dstBufferOffset < 0)
						{
							doCopy = false;
						}
					}
					// end side of laser angle
					else if (dataEndAngle >= laserEndAngleH)
					{
						var srcLengthRatio = (laserEndAngleH - dataStartAngle) / dataTotalAngle;
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
						Buffer.BlockCopy(srcBuffer, srcBufferOffset * bufferUnitSize, laserScan.Ranges, dstBufferOffset * bufferUnitSize, copyLength * bufferUnitSize);
					}
				}
			}

			DoLaserAngleFilter();

			DeviceHelper.SetCurrentTime(laserScanStamped.Time);
			PushData<messages.LaserScanStamped>(laserScanStamped);
		}

		protected override IEnumerator OnVisualize()
		{
			const float visualUpdatePeriod = 0.090f;
			const float visualDrawDuration = visualUpdatePeriod * 1.01f;

			var startAngleH = (float)horizontal.angle.min;
			var startAngleV = (float)vertical.angle.min;
			var waitForEndOfFrame = new WaitForEndOfFrame();
			var waitForSeconds = new WaitForSeconds(visualUpdatePeriod);

			var horizontalSamples = horizontal.samples;
			var rangeMin = (float)range.min;
			var rangeMax = (float)range.max;

			while (true)
			{
				yield return waitForEndOfFrame;

				var lidarSensorWorldPosition = lidarLink.position + lidarSensorInitPose.position;
				var rangeData = GetRangeData();

				for (var scanIndex = 0; scanIndex < rangeData.Length; scanIndex++)
				{
					var scanIndexH = scanIndex % horizontalSamples;
					var scanIndexV = scanIndex / horizontalSamples;
					var rayAngleH = ((laserAngleResolution.H * scanIndexH)) + startAngleH;
					var rayAngleV = ((laserAngleResolution.V * scanIndexV)) + startAngleV;

					var rayRotation = Quaternion.AngleAxis((float)rayAngleH, transform.up) * Quaternion.AngleAxis((float)rayAngleV, transform.forward) * lidarLink.forward;
					var rayStart = (rayRotation * rangeMin) + lidarSensorWorldPosition;

					var ccwIndex = (uint)(rangeData.Length - scanIndex - 1);
					var rayData = (float)rangeData[ccwIndex];

					if (rayData > 0)
					{
						var rayDistance = (rayData == Mathf.Infinity) ? rangeMax : (rayData - rangeMin);
						var rayDirection = rayRotation * rayDistance;
						Debug.DrawRay(rayStart, rayDirection, rayColor, visualDrawDuration, true);
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
				return new double[0];
			}
		}
	}
}