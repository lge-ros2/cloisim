/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private gazebo.msgs.LaserScanStamped laserScanStamped = null;

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

		private UnityEngine.Camera laserCamera = null;
		private Material depthMaterial = null;

		private const float defaultRotationOffset = 90.00000000000000f;
		private const float laserCameraHFov = 45.0000000000f;
		private const float laserCameraHFovHalf = laserCameraHFov / 2;
		private const float laserCameraVFov = 40.0000000000f;

		private float laserHAngleResolution = 0;
		private float laserVAngleResolution = 0;

		private int numberOfLaserCamData = 0;

		private LaserCamData[] laserCamData;

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

		protected override void OnStart()
		{
			laserCamera = gameObject.AddComponent<UnityEngine.Camera>();

			lidarLink = transform.parent;

			InitializeMessages();

			if (laserCamera)
			{
				SetupLaserCamera();

				SetupLaserCameraData();

				StartCoroutine(LaserCameraWorker());
			}
		}

		private void InitializeMessages()
		{
			laserScanStamped = new gazebo.msgs.LaserScanStamped();
			laserScanStamped.Time = new gazebo.msgs.Time();
			laserScanStamped.Scan = new gazebo.msgs.LaserScan();
			laserScanStamped.Scan.WorldPose = new gazebo.msgs.Pose();
			laserScanStamped.Scan.WorldPose.Position = new gazebo.msgs.Vector3d();
			laserScanStamped.Scan.WorldPose.Orientation = new gazebo.msgs.Quaternion();

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

			laserHAngleResolution = (float)(laserScan.AngleStep * Mathf.Rad2Deg);
			laserVAngleResolution = (float)(laserScan.VerticalAngleStep * Mathf.Rad2Deg);
		}

		private void SetupLaserCamera()
		{
			var depthShader = Shader.Find("Sensor/DepthShader");
			depthMaterial = new Material(depthShader);

			laserCamera.backgroundColor = Color.white;
			laserCamera.clearFlags = CameraClearFlags.SolidColor;
			laserCamera.depthTextureMode = DepthTextureMode.Depth;
			laserCamera.cullingMask = LayerMask.GetMask("Default");

			laserCamera.allowHDR = true;
			laserCamera.allowMSAA = false;
			laserCamera.renderingPath = RenderingPath.DeferredLighting;
			laserCamera.stereoTargetEye = StereoTargetEyeMask.None;

			laserCamera.orthographic = false;
			laserCamera.nearClipPlane = (float)rangeMin;
			laserCamera.farClipPlane = (float)rangeMax;
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(laserCameraHFov, laserCameraVFov, (float)rangeMin, (float)rangeMax);
			laserCamera.projectionMatrix = projMatrix;

			var renderTextrueWidth = Mathf.CeilToInt(laserCameraHFov / laserHAngleResolution);
			var aspectRatio = Mathf.Tan(laserCameraVFov / 2 * Mathf.Deg2Rad) / Mathf.Tan(laserCameraHFov / 2 * Mathf.Deg2Rad);
			var renderTextrueHeight = Mathf.RoundToInt(renderTextrueWidth * aspectRatio);
			var targetDepthRT = new RenderTexture(renderTextrueWidth, renderTextrueHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
			targetDepthRT.name = "LidarDepthTexture";
			laserCamera.targetTexture = targetDepthRT;
			laserCamera.enabled = false;

			laserCamera.hideFlags |= HideFlags.NotEditable;
		}

		private void SetupLaserCameraData()
		{
			const float laserCameraRotationAngle = laserCameraHFov;
			numberOfLaserCamData = Mathf.CeilToInt(360 / laserCameraRotationAngle);

			laserCamData = new LaserCamData[numberOfLaserCamData];

			var targetDepthRT = laserCamera.targetTexture;
			for (var index = 0; index < numberOfLaserCamData; index++)
			{
				var data = new LaserCamData();
				data.AllocateTexture(index, targetDepthRT.width, targetDepthRT.height);
				data.CenterAngle = laserCameraRotationAngle * index;
				laserCamData[index] = data;
			}

			tempLaserData = new LaserData();
		}

		private IEnumerator LaserCameraWorker()
		{
			float ScanningPeriod = (UpdatePeriod/numberOfLaserCamData);
			var axisRotation = Vector3.zero;
			var waitForSeconds = new WaitForSeconds(ScanningPeriod);

			while (true)
			{
				for (var dataIndex = 0; dataIndex < numberOfLaserCamData; dataIndex++)
				{
					var data = laserCamData[dataIndex];
					axisRotation.y = data.CenterAngle;

					laserCamera.transform.localRotation = Quaternion.Euler(axisRotation);
					laserCamera.Render();

					data.SetTextureData(laserCamera.targetTexture);

					laserCamera.enabled = false;

					yield return waitForSeconds;
				}
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

			var startAngle = defaultRotationOffset + (float)angleMin;
			for (var hScanIndex = 0; hScanIndex < laserScanStamped.Scan.Count; hScanIndex++)
			{
				var rayAngleH = ((laserHAngleResolution * hScanIndex)) + startAngle;

				var convertedRayAngleH = (rayAngleH >= -laserCameraHFovHalf) ? rayAngleH : (360 + rayAngleH);
				var dataIndexByAngle = (uint)Mathf.Round(convertedRayAngleH / laserCameraHFov);
				var laserScanData = laserCamData[dataIndexByAngle];
				var centerAngleInCamData = laserScanData.CenterAngle;

				tempLaserData.data = laserScanData.GetTextureData();
				tempLaserData.width = laserScanData.ImageWidth;
				tempLaserData.height = laserScanData.ImageHeight;

				var depthData = tempLaserData.GetDepthData(convertedRayAngleH - centerAngleInCamData);
				var rayDistance = (depthData > 0) ? depthData * (float)rangeMax : Mathf.Infinity;

				// Store the laser data CCW
				var scanIndexCcw = (uint)laserScanStamped.Scan.Count - hScanIndex - 1;
				laserScan.Ranges[scanIndexCcw] = (double)rayDistance;
				laserScan.Intensities[scanIndexCcw] = 0.0f;
			}

			DeviceHelper.SetCurrentTime(laserScanStamped.Time);
			PushData<gazebo.msgs.LaserScanStamped>(laserScanStamped);
		}

		protected override IEnumerator OnVisualize()
		{
			const float visualUpdatePeriod = 0.090f;
			const float visualDrawDuration = visualUpdatePeriod * 1.01f;
			var startAngle = defaultRotationOffset + (float)angleMin;
			var waitForSeconds = new WaitForSeconds(visualUpdatePeriod);

			while (true)
			{
				var lidarSensorWorldPosition = lidarLink.position + transform.localPosition;
				var rangeData = GetRangeData();

				for (var hScanIndex = 0; hScanIndex < rangeData.Length; hScanIndex++)
				{
					var rayAngleH = ((laserHAngleResolution * hScanIndex)) + startAngle;
					var rayRotation = Quaternion.AngleAxis((float)(rayAngleH), lidarLink.up) * lidarLink.forward;
					var rayStart = (rayRotation * (float)rangeMin) + lidarSensorWorldPosition;
					var rayDistance = (rangeData[hScanIndex] == Mathf.Infinity) ? (float)rangeMax : (rangeData[hScanIndex] - (float)rangeMin);
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
				Array.Reverse(temp);
				return temp;
			}
			catch
			{
				return new float[0];
			}
		}
	}
}