/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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

		protected UnityEngine.Camera camSensor = null;
		protected UniversalAdditionalCameraData universalCamData = null;

		protected string targetRTname;
		protected int targetRTdepth;
		protected RenderTextureFormat targetRTformat;
		protected RenderTextureReadWrite targetRTrwmode;
		protected TextureFormat readbackDstFormat;
		private CameraImageData camImageData;

		private CommandBuffer cmdBuffer;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				// This is where you can write custom rendering code. Customize this method to customize your SRP.
				// Create and schedule a command to clear the current render target
				cmdBuffer.SetInvertCulling(true);
				context.ExecuteCommandBuffer(cmdBuffer);
				// Tell the Scriptable Render Context to tell the graphics API to perform the scheduled commands
				cmdBuffer.Clear();
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				cmdBuffer.SetInvertCulling(false);
				context.ExecuteCommandBuffer(cmdBuffer);
				cmdBuffer.Clear();
				context.Submit();
			}
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			cmdBuffer = new CommandBuffer();
			camSensor = gameObject.AddComponent<UnityEngine.Camera>();
			universalCamData = camSensor.GetUniversalAdditionalCameraData();

			// for controlling targetDisplay
			camSensor.targetDisplay = -1;
			camSensor.stereoTargetEye = StereoTargetEyeMask.None;

			cameraLink = transform.parent;
		}

		protected override void OnStart()
		{
			if (camSensor)
			{
				SetupTexture();
				SetupCamera();
				StartCoroutine(CameraWorker());
			}
		}

		protected virtual void SetupTexture()
		{
			camSensor.depthTextureMode = DepthTextureMode.None;
			universalCamData.requiresColorTexture = true;
			universalCamData.requiresDepthTexture = false;
			universalCamData.renderShadows = true;

			// Debug.Log("This is not a Depth Camera!");
			targetRTname = "CameraColorTexture";
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
			camSensor.ResetWorldToCameraMatrix();
			camSensor.ResetProjectionMatrix();

			camSensor.allowHDR = true;
			camSensor.allowMSAA = true;
			camSensor.allowDynamicResolution = true;
			camSensor.useOcclusionCulling = true;

			camSensor.stereoTargetEye = StereoTargetEyeMask.None;

			camSensor.orthographic = false;
			camSensor.nearClipPlane = (float)GetParameters().clip.near;
			camSensor.farClipPlane = (float)GetParameters().clip.far;
			camSensor.cullingMask = LayerMask.GetMask("Default");

			var targetRT = new RenderTexture(GetParameters().image_width, GetParameters().image_height, targetRTdepth, targetRTformat, targetRTrwmode)
			{
				name = targetRTname,
				dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
				antiAliasing = 1,
				useMipMap = false,
				useDynamicScale = false,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				enableRandomWrite = true
			};

			camSensor.targetTexture = targetRT;

			var camHFov = (float)GetParameters().horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, camSensor.aspect);
			camSensor.fieldOfView = camVFov;

			// Invert projection matrix for cloisim msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, camSensor.nearClipPlane, camSensor.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			camSensor.projectionMatrix = projMatrix * invertMatrix;

			universalCamData.enabled = false;
			universalCamData.renderPostProcessing = true;
			camSensor.enabled = false;

			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

			camSensor.hideFlags |= HideFlags.NotEditable;

			camImageData = new CameraImageData(GetParameters().image_width, GetParameters().image_height, GetParameters().image_format);
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Camera)");
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

			base.OnDestroy();
		}

		private IEnumerator CameraWorker()
		{
			var waitForSeconds = new WaitForSeconds(WaitPeriod());

			while (true)
			{
				universalCamData.enabled = true;
				camSensor.enabled = true;

				// Debug.Log("start render and request ");
				if (camSensor.isActiveAndEnabled)
				{
					camSensor.Render();
				}
				var readback = AsyncGPUReadback.Request(camSensor.targetTexture, 0, readbackDstFormat, OnCompleteAsyncReadback);

				universalCamData.enabled = false;
				camSensor.enabled = false;

				yield return null;
				readback.WaitForCompletion();

				yield return waitForSeconds;
			}
		}

		protected void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogError("Failed to read GPU texture");
				return;
			}
			// Debug.Assert(request.done);

			if (request.done)
			{
				var readbackData = request.GetData<byte>();
				camImageData.SetTextureBufferData(readbackData);
				var image = imageStamped.Image;
				if (image.Data.Length == camImageData.GetImageDataLength())
				{
					var imageData = camImageData.GetImageData();

					PostProcessing(ref imageData);

					// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);
					image.Data = imageData;

					if (GetParameters().save_enabled)
					{
						var saveName = name + "_" + Time.time;
						camImageData.SaveRawImageData(GetParameters().save_path, saveName);
						// Debug.LogFormat("{0}|{1} captured", GetParameters().save_path, saveName);
					}
				}
				readbackData.Dispose();
			}
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(imageStamped.Time);
			PushDeviceMessage<messages.ImageStamped>(imageStamped);
		}

		protected virtual void PostProcessing(ref byte[] buffer) { }

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