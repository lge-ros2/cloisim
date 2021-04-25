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

		protected UnityEngine.Camera _cam = null;
		protected UniversalAdditionalCameraData _universalCamData = null;

		protected string targetRTname;
		protected int targetRTdepth;
		protected RenderTextureFormat targetRTformat;
		protected RenderTextureReadWrite targetRTrwmode;
		protected TextureFormat readbackDstFormat;
		private CamImageData camData;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(_cam))
			{
				// This is where you can write custom rendering code. Customize this method to customize your SRP.
				// Create and schedule a command to clear the current render target
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(true);
				context.ExecuteCommandBuffer(cmdBuffer);
				// Tell the Scriptable Render Context to tell the graphics API to perform the scheduled commands
				cmdBuffer.Release();
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(_cam))
			{
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(false);
				context.ExecuteCommandBuffer(cmdBuffer);
				cmdBuffer.Release();
				context.Submit();
			}
		}

		protected override void OnAwake()
		{
			_mode = Mode.TX;
			_cam = gameObject.AddComponent<UnityEngine.Camera>();
			_universalCamData = _cam.GetUniversalAdditionalCameraData();

			// for controlling targetDisplay
			_cam.targetDisplay = -1;
			_cam.stereoTargetEye = StereoTargetEyeMask.None;

			cameraLink = transform.parent;
		}

		protected override void OnStart()
		{
			if (_cam)
			{
				SetupTexture();
				SetupCamera();
				StartCoroutine(CameraWorker());
			}
		}

		protected virtual void SetupTexture()
		{
			_cam.depthTextureMode = DepthTextureMode.None;
			_universalCamData.requiresColorTexture = true;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = true;

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
			_cam.ResetWorldToCameraMatrix();
			_cam.ResetProjectionMatrix();

			_cam.allowHDR = true;
			_cam.allowMSAA = true;
			_cam.allowDynamicResolution = true;
			_cam.useOcclusionCulling = true;

			_cam.stereoTargetEye = StereoTargetEyeMask.None;

			_cam.orthographic = false;
			_cam.nearClipPlane = (float)GetParameters().clip.near;
			_cam.farClipPlane = (float)GetParameters().clip.far;
			_cam.cullingMask = LayerMask.GetMask("Default");

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

			_cam.targetTexture = targetRT;

			var camHFov = (float)GetParameters().horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, _cam.aspect);
			_cam.fieldOfView = camVFov;

			// Invert projection matrix for cloisim msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, _cam.nearClipPlane, _cam.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			_cam.projectionMatrix = projMatrix * invertMatrix;

			_universalCamData.enabled = false;
			_universalCamData.renderPostProcessing = true;
			_cam.enabled = false;

			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

			_cam.hideFlags |= HideFlags.NotEditable;

			camData.AllocateTexture(GetParameters().image_width, GetParameters().image_height, GetParameters().image_format);
		}
		void OnDestroy()
		{
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
		}

		private IEnumerator CameraWorker()
		{
			var waitForSeconds = new WaitForSeconds(WaitPeriod());

			while (true)
			{
				_universalCamData.enabled = true;
				_cam.enabled = true;

				// Debug.Log("start render and request ");
				if (_cam.isActiveAndEnabled)
				{
					_cam.Render();
				}
				var readback = AsyncGPUReadback.Request(_cam.targetTexture, 0, readbackDstFormat, OnCompleteAsyncReadback);

				_universalCamData.enabled = false;
				_cam.enabled = false;

				yield return null;
				readback.WaitForCompletion();

				yield return waitForSeconds;
			}
		}

		protected virtual void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogError("Failed to read GPU texture");
				return;
			}
			// Debug.Assert(request.done);

			if (request.done)
			{
				camData.SetTextureBufferData(request.GetData<byte>());
				var image = imageStamped.Image;
				if (image.Data.Length == camData.GetImageDataLength())
				{
					var imageData = camData.GetImageData();

					camData.Dispose();

					BufferDepthScaling(ref imageData);
					// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);
					image.Data = imageData;

					if (GetParameters().save_enabled)
					{
						var saveName = name + "_" + Time.time;
						camData.SaveRawImageData(GetParameters().save_path, saveName);
						// Debug.LogFormat("{0}|{1} captured", GetParameters().save_path, saveName);
					}
				}
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