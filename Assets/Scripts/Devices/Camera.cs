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
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class Camera : Device
	{
		protected SDF.Camera camParameter = null;
		protected messages.CameraSensor sensorInfo = null;
		protected messages.ImageStamped imageStamped = null;

		// TODO : Need to be implemented!!!
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
		private CameraData.ImageData camImageData;
		private CommandBuffer cmdBufferBegin;
		private CommandBuffer cmdBufferEnd;
		public Noise noise = null;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				context.ExecuteCommandBuffer(cmdBufferBegin);
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				context.ExecuteCommandBuffer(cmdBufferEnd);
				context.Submit();
			}
		}

		public void SetCamParameter(in SDF.Camera param)
		{
			camParameter = param;
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			cmdBufferBegin = new CommandBuffer();
			cmdBufferBegin.SetInvertCulling(true);

			cmdBufferEnd = new CommandBuffer();
			cmdBufferEnd.SetInvertCulling(false);

			camSensor = GetComponent<UnityEngine.Camera>();
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

			var pixelFormat = CameraData.GetPixelFormat(camParameter.image_format);
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT8:
					targetRTformat = RenderTextureFormat.R8;
					readbackDstFormat = TextureFormat.R8;
					break;

				case CameraData.PixelFormat.RGB_INT8:
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
			var pixelFormat = CameraData.GetPixelFormat(camParameter.image_format);
			image.Width = (uint)camParameter.image_width;
			image.Height = (uint)camParameter.image_height;
			image.PixelFormat = (uint)pixelFormat;
			image.Step = image.Width * (uint)CameraData.GetImageDepth(pixelFormat);
			image.Data = new byte[image.Height * image.Step];

			sensorInfo = new messages.CameraSensor();
			sensorInfo.ImageSize = new messages.Vector2d();
			sensorInfo.Distortion = new messages.Distortion();
			sensorInfo.Distortion.Center = new messages.Vector2d();

			sensorInfo.HorizontalFov = camParameter.horizontal_fov;
			sensorInfo.ImageSize.X = camParameter.image_width;
			sensorInfo.ImageSize.Y = camParameter.image_height;
			sensorInfo.ImageFormat = camParameter.image_format;
			sensorInfo.NearClip = camParameter.clip.near;
			sensorInfo.FarClip = camParameter.clip.far;
			sensorInfo.SaveEnabled = camParameter.save_enabled;
			sensorInfo.SavePath = camParameter.save_path;

			if (camParameter.distortion != null)
			{
				sensorInfo.Distortion.Center.X = camParameter.distortion.center.X;
				sensorInfo.Distortion.Center.Y = camParameter.distortion.center.Y;
				sensorInfo.Distortion.K1 = camParameter.distortion.k1;
				sensorInfo.Distortion.K2 = camParameter.distortion.k2;
				sensorInfo.Distortion.K3 = camParameter.distortion.k3;
				sensorInfo.Distortion.P1 = camParameter.distortion.p1;
				sensorInfo.Distortion.P2 = camParameter.distortion.p2;
			}
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
			camSensor.nearClipPlane = (float)camParameter.clip.near;
			camSensor.farClipPlane = (float)camParameter.clip.far;
			camSensor.cullingMask = LayerMask.GetMask("Default");

			var targetRT = new RenderTexture(camParameter.image_width, camParameter.image_height, targetRTdepth, targetRTformat, targetRTrwmode)
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

			var camHFov = (float)camParameter.horizontal_fov * Mathf.Rad2Deg;
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

			// camSensor.hideFlags |= HideFlags.NotEditable;

			camImageData = new CameraData.ImageData(camParameter.image_width, camParameter.image_height, camParameter.image_format);
		}

		protected new void OnDestroy()
		{
			StopCoroutine(CameraWorker());

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
				camSensor.enabled = true;

				// Debug.Log("start render and request ");
				if (camSensor.isActiveAndEnabled)
				{
					camSensor.Render();
				}
				var readback = AsyncGPUReadback.Request(camSensor.targetTexture, 0, readbackDstFormat, OnCompleteAsyncReadback);

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
				camImageData.SetTextureBufferData(ref readbackData);
				var image = imageStamped.Image;
				if (image.Data.Length == camImageData.GetImageDataLength())
				{
					var imageData = camImageData.GetImageData();

					PostProcessing(ref imageData);

					// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);
					image.Data = imageData;

					if (camParameter.save_enabled)
					{
						var saveName = name + "_" + Time.time;
						camImageData.SaveRawImageData(camParameter.save_path, saveName);
						// Debug.LogFormat("{0}|{1} captured", camParameter.save_path, saveName);
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
	}
}