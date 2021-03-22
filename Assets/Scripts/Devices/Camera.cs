/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class Camera : Device
	{
		protected messages.CameraSensor sensorInfo = null;
		protected messages.ImageStamped imageStamped = null;

		// public SDF.Camera parameters = null;
		// TODO : Need to be implemented!!!
		// <noise> TBD
		// <lens> TBD
		// <distortion> TBD

		protected Transform cameraLink = null;

		protected UnityEngine.Camera cam = null;

		public bool runningDeviceWork = true;

		protected string targetRTname;
		protected int targetRTdepth;
		protected RenderTextureFormat targetRTformat;
		protected RenderTextureReadWrite targetRTrwmode;
		protected TextureFormat readbackDstFormat;
		private CamImageData camData;

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

			// for controlling targetDisplay
			cam.targetDisplay = -1;
			cam.stereoTargetEye = StereoTargetEyeMask.None;

			cameraLink = transform.parent;
		}

		protected override void OnStart()
		{
			if (cam)
			{
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
			cam.depthTextureMode = DepthTextureMode.None;

			// Debug.Log("This is not a Depth Camera!");
			targetRTname = "CameraTexture";
			targetRTdepth = 0;
			targetRTrwmode = RenderTextureReadWrite.sRGB;

			var pixelFormat = GetPixelFormat(GetParameters().image_format);
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
			var pixelFormat = GetPixelFormat(GetParameters().image_format);
			image.Width = (uint)GetParameters().image_width;
			image.Height = (uint)GetParameters().image_height;
			image.PixelFormat = (uint)pixelFormat;
			image.Step = image.Width * (uint)GetImageDepth(pixelFormat);
			image.Data = new byte[image.Height * image.Step];

			sensorInfo = new messages.CameraSensor();
			sensorInfo.ImageSize = new messages.Vector2d();
			sensorInfo.Distortion = new messages.Distortion();
			sensorInfo.Distortion.Center = new messages.Vector2d();

			sensorInfo.HorizontalFov = GetParameters().horizontal_fov;
			sensorInfo.ImageSize.X = GetParameters().image_width;
			sensorInfo.ImageSize.Y = GetParameters().image_height;
			sensorInfo.ImageFormat = GetParameters().image_format;
			sensorInfo.NearClip = GetParameters().clip.near;
			sensorInfo.FarClip = GetParameters().clip.far;
			sensorInfo.SaveEnabled = GetParameters().save_enabled;
			sensorInfo.SavePath = GetParameters().save_path;
			sensorInfo.Distortion.Center.X = GetParameters().distortion.center.X;
			sensorInfo.Distortion.Center.Y = GetParameters().distortion.center.Y;
			sensorInfo.Distortion.K1 = GetParameters().distortion.k1;
			sensorInfo.Distortion.K2 = GetParameters().distortion.k2;
			sensorInfo.Distortion.K3 = GetParameters().distortion.k3;
			sensorInfo.Distortion.P1 = GetParameters().distortion.p1;
			sensorInfo.Distortion.P2 = GetParameters().distortion.p2;
		}

		private void SetupCamera()
		{
			cam.ResetWorldToCameraMatrix();
			cam.ResetProjectionMatrix();

			cam.allowHDR = true;
			cam.allowMSAA = true;
			cam.allowDynamicResolution = true;
			cam.useOcclusionCulling = true;

			cam.stereoTargetEye = StereoTargetEyeMask.None;

			cam.orthographic = false;
			cam.nearClipPlane = (float)GetParameters().clip.near;
			cam.farClipPlane = (float)GetParameters().clip.far;
			cam.cullingMask = LayerMask.GetMask("Default");

			var targetRT = new RenderTexture(GetParameters().image_width, GetParameters().image_height, targetRTdepth, targetRTformat, targetRTrwmode)
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

			var camHFov = (float)GetParameters().horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, cam.aspect);
			cam.fieldOfView = camVFov;

			// Invert projection matrix for cloisim msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, cam.nearClipPlane, cam.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			cam.projectionMatrix = projMatrix * invertMatrix;

			cam.enabled = false;
			// cam.hideFlags |= HideFlags.NotEditable;

			camData.AllocateTexture(GetParameters().image_width, GetParameters().image_height, GetParameters().image_format);
		}

		private IEnumerator CameraWorker()
		{
			var image = imageStamped.Image;
			var waitForSeconds = new WaitForSeconds(WaitPeriod());

			while (true)
			{
				cam.enabled = true;

				if (cam.isActiveAndEnabled)
				{
					cam.Render();
				}
				var readback = AsyncGPUReadback.Request(cam.targetTexture, 0, readbackDstFormat);

				cam.enabled = false;

				yield return null;

				readback.WaitForCompletion();

				if (readback.hasError)
				{
					Debug.LogError("Failed to read GPU texture");
					continue;
				}
				// Debug.Assert(request.done);

				if (readback.done)
				{
					camData.SetTextureBufferData(readback.GetData<byte>());

					if (image.Data.Length == camData.GetImageDataLength())
					{
						// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);
						image.Data = camData.GetImageData();

						if (GetParameters().save_enabled)
						{
							var saveName = name + "_" + Time.time;
							camData.SaveRawImageData(GetParameters().save_path, saveName);
							// Debug.LogFormat("{0}|{1} captured", GetParameters().save_path, saveName);
						}
					}
				}

				yield return waitForSeconds;
			}
		}

		protected override IEnumerator DeviceCoroutine()
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
			DeviceHelper.SetCurrentTime(imageStamped.Time);
			PushData<messages.ImageStamped>(imageStamped);
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return sensorInfo;
		}

		public messages.Image GetImageDataMessage()
		{
			return (imageStamped == null || imageStamped.Image == null)? null:imageStamped.Image;
		}

		public SDF.Camera GetParameters()
		{
			return deviceParameters as SDF.Camera;
		}
	}
}