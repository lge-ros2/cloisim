/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using messages = cloisim.msgs;
using Unity.Collections;

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
		protected UniversalAdditionalCameraData _universalCamData = null;

		protected string targetRTname;
		protected GraphicsFormat targetColorFormat;
		protected GraphicsFormat readbackDstFormat;

		private CameraData.Image camImageData;
		private List<AsyncGPUReadbackRequest> _readbackList = new List<AsyncGPUReadbackRequest>();
		public Noise noise = null;
		protected bool _startCameraWork = false;
		private float _lastTimeCameraWork = 0f;
		private RTHandle _rtHandle;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(true);
				context.ExecuteCommandBuffer(cmdBuffer);
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(camSensor))
			{
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(false);
				context.ExecuteCommandBuffer(cmdBuffer);
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

			camSensor = GetComponent<UnityEngine.Camera>();
			_universalCamData = camSensor.GetUniversalAdditionalCameraData();

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
				_startCameraWork = true;
			}
		}

		protected virtual void SetupTexture()
		{
			camSensor.clearFlags = CameraClearFlags.Skybox;
			camSensor.allowHDR = true;
			camSensor.depthTextureMode = DepthTextureMode.None;
			_universalCamData.requiresColorOption = CameraOverrideOption.On;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = true;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = true;

			// Debug.Log("This is not a Depth Camera!");
			targetRTname = "CameraColorTexture";

			var pixelFormat = CameraData.GetPixelFormat(camParameter.image.format);
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT8:
					targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					readbackDstFormat = GraphicsFormat.R8_SRGB;
					break;

				case CameraData.PixelFormat.RGB_INT8:
				default:
					targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					readbackDstFormat = GraphicsFormat.R8G8B8_SRGB;
					break;
			}

			camImageData = new CameraData.Image(camParameter.image.width, camParameter.image.height, pixelFormat);
		}

		protected override void InitializeMessages()
		{
			imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Image = new messages.Image();

			sensorInfo = new messages.CameraSensor();
			sensorInfo.ImageSize = new messages.Vector2d();
			sensorInfo.Distortion = new messages.Distortion();
			sensorInfo.Distortion.Center = new messages.Vector2d();
		}

		protected override void SetupMessages()
		{
			var image = imageStamped.Image;
			var pixelFormat = CameraData.GetPixelFormat(camParameter.image.format);
			image.Width = (uint)camParameter.image.width;
			image.Height = (uint)camParameter.image.height;
			image.PixelFormat = (uint)pixelFormat;
			image.Step = image.Width * (uint)CameraData.GetImageDepth(pixelFormat);
			image.Data = new byte[image.Height * image.Step];

			sensorInfo.HorizontalFov = camParameter.horizontal_fov;
			sensorInfo.ImageSize.X = camParameter.image.width;
			sensorInfo.ImageSize.Y = camParameter.image.height;
			sensorInfo.ImageFormat = camParameter.image.format;
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

			camSensor.renderingPath = RenderingPath.Forward;
			camSensor.allowMSAA = true;
			camSensor.allowDynamicResolution = true;
			camSensor.useOcclusionCulling = true;
			camSensor.stereoTargetEye = StereoTargetEyeMask.None;
			camSensor.orthographic = false;
			camSensor.nearClipPlane = (float)camParameter.clip.near;
			camSensor.farClipPlane = (float)camParameter.clip.far;
			camSensor.cullingMask = LayerMask.GetMask("Default");

			RTHandles.SetHardwareDynamicResolutionState(true);
			_rtHandle = RTHandles.Alloc(
				width: camParameter.image.width,
				height: camParameter.image.height,
				slices: 1,
				depthBufferBits: DepthBits.None,
				colorFormat: targetColorFormat,
				filterMode: FilterMode.Trilinear,
				wrapMode: TextureWrapMode.Clamp,
				dimension: TextureDimension.Tex2D,
				msaaSamples: MSAASamples.MSAA4x,
				enableRandomWrite: false,
				useMipMap: true,
				autoGenerateMips: true,
				isShadowMap: false,
				anisoLevel: 2,
				mipMapBias: 0,
				bindTextureMS: false,
				useDynamicScale: true,
				memoryless: RenderTextureMemoryless.None,
				name: targetRTname);

			camSensor.targetTexture = _rtHandle.rt;

			var camHFov = (float)camParameter.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, camSensor.aspect);
			camSensor.fieldOfView = camVFov;

			// Invert projection matrix for cloisim msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, camSensor.nearClipPlane, camSensor.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			camSensor.projectionMatrix = projMatrix * invertMatrix;

			_universalCamData.enabled = false;
			_universalCamData.stopNaN = true;
			_universalCamData.dithering = true;
			_universalCamData.renderPostProcessing = false;
			_universalCamData.allowXRRendering = false;
			_universalCamData.volumeLayerMask = LayerMask.GetMask("Nothing");
			_universalCamData.renderType = CameraRenderType.Base;
			_universalCamData.cameraStack.Clear();
			camSensor.enabled = false;

			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;

			// camSensor.hideFlags |= HideFlags.NotEditable;
		}

		protected new void OnDestroy()
		{
			_startCameraWork = false;

			// Debug.Log("OnDestroy(Camera)");
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

			_rtHandle?.Release();

			base.OnDestroy();
		}

		void Update()
		{
			if (_startCameraWork)
			{
				CameraWorker();
			}
		}

		private void CameraWorker()
		{
			if (Time.time - _lastTimeCameraWork >= WaitPeriod(0.0001f))
			{
				_universalCamData.enabled = true;

				// Debug.Log("start render and request ");
				if (_universalCamData.isActiveAndEnabled)
				{
					camSensor.Render();

					var readbackRequest = AsyncGPUReadback.Request(camSensor.targetTexture, 0, readbackDstFormat, OnCompleteAsyncReadback);

					lock (_readbackList)
					{
						_readbackList.Add(readbackRequest);
					}
				}

				_universalCamData.enabled = false;

				_lastTimeCameraWork = Time.time;
			}
		}

		protected void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogErrorFormat("{0}: Failed to read GPU texture", name);
			}
			else if (request.done)
			{
				checked
				{
					var readbackData = request.GetData<byte>();
					ImageProcessing(ref readbackData);
					readbackData.Dispose();
				}

				lock (_readbackList)
				{
					_readbackList.Remove(request);
				}
			}
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(imageStamped.Time);
			PushDeviceMessage<messages.ImageStamped>(imageStamped);
		}

		protected virtual void ImageProcessing(ref NativeArray<byte> readbackData)
		{
			var image = imageStamped.Image;
			camImageData.SetTextureBufferData(readbackData);

			var imageData = camImageData.GetImageData(image.Data.Length);
			if (imageData != null)
			{
				image.Data = imageData;
				if (camParameter.save_enabled && _startCameraWork)
				{
					var saveName = name + "_" + Time.time;
					camImageData.SaveRawImageData(camParameter.save_path, saveName);
					// Debug.LogFormat("{0}|{1} captured", camParameter.save_path, saveName);
				}
			}
			else
			{
				Debug.LogWarningFormat("{0}: Failed to get image Data", name);
			}
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return sensorInfo;
		}

		public messages.Image GetImageDataMessage()
		{
			return (imageStamped == null || imageStamped.Image == null) ? null : imageStamped.Image;
		}
	}
}