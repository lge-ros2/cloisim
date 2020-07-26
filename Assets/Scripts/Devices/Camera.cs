/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public partial class Camera : Device
	{
		protected messages.CameraSensor sensorInfo = null;
		protected messages.ImageStamped imageStamped = null;

		public SDF.Camera parameters = null;

		// TODO : Need to be implemented!!!
		// <noise> TBD
		// <lens> TBD
		// <distortion> TBD

		protected Transform cameraLink = null;

		protected UnityEngine.Camera cam = null;

		public float adjustCapturingRate = 0.95f;

		public bool runningDeviceWork = true;

		protected string targetRTname;
		protected int targetRTdepth;
		protected RenderTextureFormat targetRTformat;
		protected RenderTextureReadWrite targetRTrwmode;
		protected TextureFormat readbackDstFormat;

		void OnPreRender()
		{
			GL.invertCulling = true;
		}

		void OnPostRender()
		{
			GL.invertCulling = false;
		}

		protected override void OnAwake()
		{
			cam = gameObject.AddComponent<UnityEngine.Camera>();
			cameraLink = transform.parent;
		}

		protected override void OnStart()
		{
			if (cam)
			{
				cam.transform.Rotate(Vector3.up, 90.0000000000f);

				SetupTexture();
				SetupCamera();
				StartCoroutine(CameraWorker());
			}
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
		}

		protected virtual void SetupTexture()
		{
			// Debug.Log("This is not a Depth Camera!");
			targetRTname = "CameraTexture";
			targetRTdepth = 0;
			targetRTrwmode = RenderTextureReadWrite.sRGB;

			var pixelFormat = GetPixelFormat(parameters.image_format);
			switch (pixelFormat)
			{
				case PixelFormat.L_INT8:
					targetRTformat = RenderTextureFormat.R8;
					readbackDstFormat = TextureFormat.R8;
					break;

				case PixelFormat.RGB_INT8:
				default:
					targetRTformat = RenderTextureFormat.ARGB32;
					readbackDstFormat = TextureFormat.RGB24;
					break;
			}
		}

		protected override void InitializeMessages()
		{
			imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Image = new messages.Image();

			var image = imageStamped.Image;
			image.Width = (uint)parameters.image_width;
			image.Height = (uint)parameters.image_height;
			image.PixelFormat = (uint)GetPixelFormat(parameters.image_format);
			image.Step = image.Width * (uint)GetImageDepth(parameters.image_format);
			image.Data = new byte[image.Height * image.Step];

			sensorInfo = new messages.CameraSensor();
			sensorInfo.ImageSize = new messages.Vector2d();
			sensorInfo.Distortion = new messages.Distortion();
			sensorInfo.Distortion.Center = new messages.Vector2d();

			sensorInfo.HorizontalFov = parameters.horizontal_fov;
			sensorInfo.ImageSize.X = parameters.image_width;
			sensorInfo.ImageSize.Y = parameters.image_height;
			sensorInfo.ImageFormat = parameters.image_format;
			sensorInfo.NearClip = parameters.clip.near;
			sensorInfo.FarClip = parameters.clip.far;
			sensorInfo.SaveEnabled = parameters.save_enabled;
			sensorInfo.SavePath = parameters.save_path;
			sensorInfo.Distortion.Center.X = parameters.distortion.center.X;
			sensorInfo.Distortion.Center.Y = parameters.distortion.center.Y;
			sensorInfo.Distortion.K1 = parameters.distortion.k1;
			sensorInfo.Distortion.K2 = parameters.distortion.k2;
			sensorInfo.Distortion.K3 = parameters.distortion.k3;
			sensorInfo.Distortion.P1 = parameters.distortion.p1;
			sensorInfo.Distortion.P2 = parameters.distortion.p2;
		}

		private void SetupCamera()
		{
			cam.ResetWorldToCameraMatrix();
			cam.ResetProjectionMatrix();

			cam.allowHDR = true;
			cam.allowMSAA = false;
			cam.allowDynamicResolution = true;
			cam.useOcclusionCulling = true;
			cam.targetDisplay = 0;
			cam.stereoTargetEye = StereoTargetEyeMask.None;

			cam.orthographic = false;
			cam.nearClipPlane = (float)parameters.clip.near;
			cam.farClipPlane = (float)parameters.clip.far;
			cam.cullingMask = LayerMask.GetMask("Default");

			var targetRT = new RenderTexture(parameters.image_width, parameters.image_height, targetRTdepth, targetRTformat, targetRTrwmode)
			{
				name = targetRTname,
				dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
				antiAliasing = 1,
				useMipMap = false,
				useDynamicScale = false,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
			};

			cam.targetTexture = targetRT;

			var camHFov = (float)parameters.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, cam.aspect);
			cam.fieldOfView = camVFov;

			// Invert projection matrix for gazebo msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, cam.nearClipPlane, cam.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			cam.projectionMatrix = projMatrix * invertMatrix;
			cam.enabled = false;
			// cam.hideFlags |= HideFlags.NotEditable;

			camData.AllocateTexture(parameters.image_width, parameters.image_height, parameters.image_format);
		}

		private IEnumerator CameraWorker()
		{
			var waitForSeconds = new WaitForSeconds(UpdatePeriod * adjustCapturingRate);

			while (true)
			{
				cam.enabled = true;

				cam.Render();

				var readback = AsyncGPUReadback.Request(cam.targetTexture, 0, readbackDstFormat);

				yield return new WaitUntil(() => readback.done);

				cam.enabled = false;

				if (readback.hasError)
				{
					Debug.LogError("Failed to read GPU texture");
					continue;
				}
				// Debug.Assert(request.done);

				camData.SetTextureData(readback.GetData<byte>());

				if (parameters.save_enabled)
				{
					var saveName = name + "_" + Time.time;
					camData.SaveRawImageData(parameters.save_path, saveName);
					// Debug.LogFormat("{0}|{1} captured", parameters.save_path, saveName);
				}

				yield return waitForSeconds;
			}
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();
			while (runningDeviceWork)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var image = imageStamped.Image;
			var imageData = camData.GetTextureData();
			if (imageData != null && image.Data.Length == imageData.Length)
			{
				image.Data = imageData;
			}
			// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);

			DeviceHelper.SetCurrentTime(imageStamped.Time);
			PushData<messages.ImageStamped>(imageStamped);
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return sensorInfo;
		}
	}
}