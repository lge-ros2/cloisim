/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private messages.LaserScanStamped laserScanStamped = null;

		[Range(1, 2000)]
		public uint samples = 0;

		[Range(0, 100)]
		public double rangeMin = 0.0f;

		[Range(0, 100)]
		public double rangeMax = 0.0f;

		[Range(-180, 0)]
		public double angleMin = 0.0f;

		[Range(0, 180)]
		public double angleMax = 0.0f;
		public double resolution = 1;

		public uint verticalSamples = 1;

		[Range(-30, 0)]
		public double verticalAngleMin = 0;

		[Range(0, 30)]
		public double verticalAngleMax = 0;

		[ColorUsage(true)]
		public Color rayColor = new Color(1, 0.1f, 0.1f, 0.2f);

		private Transform lidarLink = null;
		private UnityEngine.Camera laserCam = null;
		private Material depthMaterial = null;

		private const float defaultRotationOffset = 90.00000000000000f;
		private const float laserCameraHFov = 120.0000000000f;
		private const float laserCameraHFovHalf = laserCameraHFov / 2;
		private const float laserCameraVFov = 40.0000000000f;

		private float laserHAngleResolution = 0;
		private float laserVAngleResolution = 0;

		private int numberOfLaserCamData = 0;

		private LaserCamData[] laserCamData;

		private float laserStartAngle;
		private float laserEndAngle;
		private float laserTotalAngle;

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (depthMaterial)
			{
				Graphics.Blit(source, destination, depthMaterial);
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		private double GetAngleStep(in double minAngle, in double maxAngle, in uint totalSamples)
		{
			return (maxAngle - minAngle) / (resolution * (totalSamples - 1));
		}

		protected override void OnAwake()
		{
			lidarLink = transform.parent;
		}

		protected override void OnStart()
		{
			laserCam = gameObject.AddComponent<UnityEngine.Camera>();

			if (laserCam)
			{
				SetupLaserCamera();

				SetupLaserCameraData();

				StartCoroutine(LaserCameraWorker());
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
			laserScan.AngleMin = angleMin * Mathf.Deg2Rad;
			laserScan.AngleMax = angleMax * Mathf.Deg2Rad;
			laserScan.AngleStep = GetAngleStep(laserScan.AngleMin, laserScan.AngleMax, samples);
			laserScan.RangeMin = rangeMin;
			laserScan.RangeMax = rangeMax;
			laserScan.Count = samples;

			laserScan.VerticalAngleMin = verticalAngleMin * Mathf.Deg2Rad;
			laserScan.VerticalAngleMax = verticalAngleMax * Mathf.Deg2Rad;
			laserScan.VerticalCount = verticalSamples;
			laserScan.VerticalAngleStep
				= (verticalAngleMin == 0 && verticalAngleMax == 0) ?
					1 : GetAngleStep(laserScan.VerticalAngleMin, laserScan.VerticalAngleMax, verticalSamples);

			laserScan.Ranges = new double[samples];
			laserScan.Intensities = new double[samples];
			Array.Clear(laserScan.Ranges, 0, (int)samples);
			Array.Clear(laserScan.Intensities, 0, (int)samples);

			laserHAngleResolution = (float)(laserScan.AngleStep * Mathf.Rad2Deg);
			laserVAngleResolution = (float)(laserScan.VerticalAngleStep * Mathf.Rad2Deg);
		}

		private void SetupLaserCamera()
		{
			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);

			laserCam.ResetWorldToCameraMatrix();
			laserCam.ResetProjectionMatrix();

			laserCam.allowHDR = true;
			laserCam.allowMSAA = false;
			laserCam.allowDynamicResolution = true;
			laserCam.useOcclusionCulling = true;

			laserCam.stereoTargetEye = StereoTargetEyeMask.None;

			laserCam.orthographic = false;
			laserCam.nearClipPlane = (float)rangeMin;
			laserCam.farClipPlane = (float)rangeMax;
			laserCam.cullingMask = LayerMask.GetMask("Default");

			laserCam.backgroundColor = Color.white;
			laserCam.clearFlags = CameraClearFlags.SolidColor;
			laserCam.depthTextureMode = DepthTextureMode.Depth;

			laserCam.renderingPath = RenderingPath.DeferredLighting;

			var renderTextrueWidth = Mathf.CeilToInt(laserCameraHFov / laserHAngleResolution);
			var aspectRatio = Mathf.Tan(laserCameraVFov / 2 * Mathf.Deg2Rad) / Mathf.Tan(laserCameraHFov / 2 * Mathf.Deg2Rad);
			var renderTextrueHeight = Mathf.CeilToInt(renderTextrueWidth * aspectRatio);
			var targetDepthRT = new RenderTexture(renderTextrueWidth, renderTextrueHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
			{
				name = "LidarDepthTexture"
			};

			laserCam.targetTexture = targetDepthRT;

			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(laserCameraHFov, laserCameraVFov, (float)rangeMin, (float)rangeMax);
			laserCam.projectionMatrix = projMatrix;

			laserCam.enabled = false;

			// laserCam.hideFlags |= HideFlags.NotEditable;
		}

		private void SetupLaserCameraData()
		{
			const float laserCameraRotationAngle = laserCameraHFov;
			numberOfLaserCamData = Mathf.CeilToInt(360 / laserCameraRotationAngle);

			laserCamData = new LaserCamData[numberOfLaserCamData];

			var targetDepthRT = laserCam.targetTexture;
			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				var data = new LaserCamData();
				data.angleResolutionH = laserHAngleResolution;
				data.maxHAngleHalf = laserCameraHFovHalf;
				data.maxHAngleHalfTangent = Mathf.Tan(laserCameraHFovHalf * Mathf.Deg2Rad);
				data.centerAngle = laserCameraRotationAngle * index;
				data.AllocateBuffer(targetDepthRT.width, targetDepthRT.height);
				laserCamData[index] = data;
			}

			laserStartAngle = defaultRotationOffset + (float)angleMin;
			laserEndAngle = defaultRotationOffset + (float)angleMax;
			laserTotalAngle = (float)(angleMax - angleMin);

			waitingPeriodRatio = 0.85f;
		}

		private IEnumerator LaserCameraWorker()
		{
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

					laserCam.transform.localRotation = Quaternion.Euler(axisRotation);

					laserCam.enabled = true;
					laserCam.Render();

					readbacks[dataIndex] = AsyncGPUReadback.Request(laserCam.targetTexture, 0, TextureFormat.RGBA32);

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
						var data = laserCamData[dataIndex];
						data.SetBufferData(readback.GetData<byte>());
						data.ResolveLaserRanges((float)rangeMax);
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

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();
			while (true)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();
				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var lidarPosition = lidarLink.position + transform.localPosition;
			var lidarRotation = lidarLink.rotation;

			var laserScan = laserScanStamped.Scan;

			DeviceHelper.SetVector3d(laserScan.WorldPose.Position, lidarPosition);
			DeviceHelper.SetQuaternion(laserScan.WorldPose.Orientation, lidarRotation);

			var srcBufferOffset = 0;
			var dstBufferOffset = 0;
			var copyLength = 0;
			var doCopy = true;

			for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
			{
				doCopy = true;
				var data = laserCamData[dataIndex];
				var outputBufferLength = data.output.Length;
				var dataStartAngle = data.StartAngle;
				var dataEndAngle = data.EndAngle;
				var dataTotalAngle = dataEndAngle - dataStartAngle;

				// start side of laser angle
				if (dataStartAngle < laserStartAngle)
				{
					srcBufferOffset = 0;
					var srcLengthratio = Mathf.Abs((dataStartAngle - laserStartAngle) / dataTotalAngle);
					copyLength = outputBufferLength - Mathf.FloorToInt(outputBufferLength * srcLengthratio);
					dstBufferOffset = (int)samples - copyLength;
				}
				// middle of laser angle
				else if (dataStartAngle >= laserStartAngle && dataEndAngle < laserEndAngle)
				{
					srcBufferOffset = 0;
					copyLength = outputBufferLength;
					dstBufferOffset = (int)samples - (Mathf.CeilToInt(samples * ((dataStartAngle - laserStartAngle) / laserTotalAngle)) + copyLength);
				}
				// end side of laser angle
				else if (dataEndAngle > laserEndAngle)
				{
					var srcLengthRatio = (laserEndAngle - dataStartAngle) / dataTotalAngle;
					copyLength = Mathf.FloorToInt(outputBufferLength * srcLengthRatio);
					srcBufferOffset = outputBufferLength - copyLength;
					dstBufferOffset = 0;
				}
				else
				{
					Debug.LogWarning("Something wrong data in Laser....");
					doCopy = false;
				}

				if (doCopy)
				{
					// Store CCW direction for ROS2 sensor data
					Array.Copy(data.output, srcBufferOffset, laserScan.Ranges, dstBufferOffset, copyLength);
				}
			}

			DeviceHelper.SetCurrentTime(laserScanStamped.Time);
			PushData<messages.LaserScanStamped>(laserScanStamped);
		}

		protected override IEnumerator OnVisualize()
		{
			const float visualUpdatePeriod = 0.090f;
			const float visualDrawDuration = visualUpdatePeriod * 1.01f;

			var startAngle = defaultRotationOffset + (float)angleMin;
			var waitForEndOfFrame = new WaitForEndOfFrame();
			var waitForSeconds = new WaitForSeconds(visualUpdatePeriod);

			while (true)
			{
				yield return waitForEndOfFrame;

				var lidarSensorWorldPosition = lidarLink.position + transform.localPosition;
				var rangeData = GetRangeData();

				for (var hScanIndex = 0; hScanIndex < rangeData.Length; hScanIndex++)
				{
					var rayAngleH = ((laserHAngleResolution * hScanIndex)) + startAngle;
					var rayRotation = Quaternion.AngleAxis((float)(rayAngleH), lidarLink.up) * lidarLink.forward;
					var rayStart = (rayRotation * (float)rangeMin) + lidarSensorWorldPosition;

					var ccwIndex = (uint)(rangeData.Length - hScanIndex - 1);
					var rayData = rangeData[ccwIndex];

					var rayDistance = (rayData == Mathf.Infinity) ? (float)rangeMax : (rayData - (float)rangeMin);
					var rayDirection = rayRotation * rayDistance;

					Debug.DrawRay(rayStart, rayDirection, rayColor, visualDrawDuration, true);
				}

				yield return waitForSeconds;
			}
		}

		public float[] GetRangeData()
		{
			try
			{
				var temp = Array.ConvertAll(laserScanStamped.Scan.Ranges, item => (float)item);
				return temp;
			}
			catch
			{
				return new float[0];
			}
		}
	}
}