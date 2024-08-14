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
		protected SDF.Camera _camParam = null;
		protected messages.CameraSensor _sensorInfo = null;
		protected messages.ImageStamped _imageStamped = null;

		// TODO : Need to be implemented!!!
		// <lens> TBD
		// <distortion> TBD

		protected UnityEngine.Camera _camSensor = null;
		protected UniversalAdditionalCameraData _universalCamData = null;

		protected string _targetRTname;
		protected GraphicsFormat _targetColorFormat;
		protected GraphicsFormat _readbackDstFormat;

		protected CameraData.Image _camImageData;
		private List<AsyncGPUReadbackRequest> _readbackList = new List<AsyncGPUReadbackRequest>();
		public Noise noise = null;
		protected bool _startCameraWork = false;
		private float _lastTimeCameraWork = 0f;
		private RTHandle _rtHandle;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(_camSensor))
			{
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(true);
				context.ExecuteCommandBuffer(cmdBuffer);
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera.Equals(_camSensor))
			{
				var cmdBuffer = new CommandBuffer();
				cmdBuffer.SetInvertCulling(false);
				context.ExecuteCommandBuffer(cmdBuffer);
				context.Submit();
			}
		}

		public void SetParameter(in SDF.Camera param)
		{
			_camParam = param;
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;

			_camSensor = GetComponent<UnityEngine.Camera>();
			_universalCamData = _camSensor.GetUniversalAdditionalCameraData();

			// for controlling targetDisplay
			_camSensor.targetDisplay = -1;
			_camSensor.stereoTargetEye = StereoTargetEyeMask.None;
		}

		protected override void OnStart()
		{
			if (_camSensor)
			{
				SetupTexture();
				SetupDefaultCamera();
				SetupCamera();
				_startCameraWork = true;
			}
		}

		protected override void OnReset()
		{
			lock (_readbackList)
			{
				_readbackList.Clear();
			}
		}

		protected virtual void SetupTexture()
		{
			// Debug.Log("This is not a Depth Camera!");
			_targetRTname = "CameraColorTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT8:
					_targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					_readbackDstFormat = GraphicsFormat.R8_SRGB;
					break;

				case CameraData.PixelFormat.RGB_INT8:
				default:
					_targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					_readbackDstFormat = GraphicsFormat.R8G8B8_SRGB;
					break;
			}

			_camImageData = new CameraData.Image(_camParam.image.width, _camParam.image.height, pixelFormat);
		}

		protected override void InitializeMessages()
		{
			_imageStamped = new messages.ImageStamped();
			_imageStamped.Time = new messages.Time();
			_imageStamped.Image = new messages.Image();

			_sensorInfo = new messages.CameraSensor();
			_sensorInfo.ImageSize = new messages.Vector2d();
			_sensorInfo.Distortion = new messages.Distortion();
			_sensorInfo.Distortion.Center = new messages.Vector2d();
		}

		protected override void SetupMessages()
		{
			var image = _imageStamped.Image;
			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			image.Width = (uint)_camParam.image.width;
			image.Height = (uint)_camParam.image.height;
			image.PixelFormat = (uint)pixelFormat;
			image.Step = image.Width * (uint)CameraData.GetImageStep(pixelFormat);
			image.Data = new byte[image.Height * image.Step];

			_sensorInfo.HorizontalFov = _camParam.horizontal_fov;
			_sensorInfo.ImageSize.X = _camParam.image.width;
			_sensorInfo.ImageSize.Y = _camParam.image.height;
			_sensorInfo.ImageFormat = _camParam.image.format;
			_sensorInfo.NearClip = _camParam.clip.near;
			_sensorInfo.FarClip = _camParam.clip.far;
			_sensorInfo.SaveEnabled = _camParam.save_enabled;
			_sensorInfo.SavePath = _camParam.save_path;

			if (_camParam.distortion != null)
			{
				_sensorInfo.Distortion.Center.X = _camParam.distortion.center.X;
				_sensorInfo.Distortion.Center.Y = _camParam.distortion.center.Y;
				_sensorInfo.Distortion.K1 = _camParam.distortion.k1;
				_sensorInfo.Distortion.K2 = _camParam.distortion.k2;
				_sensorInfo.Distortion.K3 = _camParam.distortion.k3;
				_sensorInfo.Distortion.P1 = _camParam.distortion.p1;
				_sensorInfo.Distortion.P2 = _camParam.distortion.p2;
			}
		}

		private void OnEnable()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
		}

		private void SetupDefaultCamera()
		{
			_camSensor.ResetWorldToCameraMatrix();
			_camSensor.ResetProjectionMatrix();

			_camSensor.backgroundColor = Color.black;
			_camSensor.clearFlags = CameraClearFlags.Nothing;
			_camSensor.depthTextureMode = DepthTextureMode.None;
			_camSensor.renderingPath = RenderingPath.Forward;
			_camSensor.allowHDR = true;
			_camSensor.allowMSAA = true;
			_camSensor.allowDynamicResolution = true;
			_camSensor.useOcclusionCulling = true;
			_camSensor.stereoTargetEye = StereoTargetEyeMask.None;
			_camSensor.orthographic = false;
			_camSensor.nearClipPlane = (float)_camParam.clip.near;
			_camSensor.farClipPlane = (float)_camParam.clip.far;
			_camSensor.cullingMask = LayerMask.GetMask("Default");

			RTHandles.SetHardwareDynamicResolutionState(true);
			_rtHandle = RTHandles.Alloc(
				width: _camParam.image.width,
				height: _camParam.image.height,
				slices: 1,
				depthBufferBits: DepthBits.None,
				colorFormat: _targetColorFormat,
				filterMode: FilterMode.Bilinear,
				wrapMode: TextureWrapMode.Clamp,
				dimension: TextureDimension.Tex2D,
				msaaSamples: MSAASamples.MSAA2x,
				enableRandomWrite: false,
				useMipMap: true,
				autoGenerateMips: true,
				isShadowMap: false,
				anisoLevel: 3,
				mipMapBias: 0,
				bindTextureMS: false,
				useDynamicScale: true,
				memoryless: RenderTextureMemoryless.None,
				name: _targetRTname);

			_camSensor.targetTexture = _rtHandle.rt;

			var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = SensorHelper.HorizontalToVerticalFOV(camHFov, _camSensor.aspect);
			_camSensor.fieldOfView = camVFov;

			// Invert projection matrix for cloisim msg
			var projMatrix = SensorHelper.MakeCustomProjectionMatrix(camHFov, camVFov, _camSensor.nearClipPlane, _camSensor.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			_camSensor.projectionMatrix = projMatrix * invertMatrix;

			SetDefaultUniversalAdditionalCameraData();

			_camSensor.enabled = false;
			// _camSensor.hideFlags |= HideFlags.NotEditable;
		}

		private void SetDefaultUniversalAdditionalCameraData()
		{
			_universalCamData.requiresColorOption = CameraOverrideOption.On;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = true;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = true;
			_universalCamData.enabled = false;
			_universalCamData.stopNaN = true;
			_universalCamData.dithering = true;
			_universalCamData.renderPostProcessing = false;
			_universalCamData.allowXRRendering = false;
			_universalCamData.volumeLayerMask = LayerMask.GetMask("Nothing");
			_universalCamData.renderType = CameraRenderType.Base;
			_universalCamData.cameraStack.Clear();
		}

		protected virtual void SetupCamera()
		{
			// Debug.Log("Base Setup Camera");
			_camSensor.clearFlags = CameraClearFlags.Skybox;
		}

		protected new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Camera)");
			_startCameraWork = false;
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
			if (Time.time - _lastTimeCameraWork >= WaitPeriod(0.00001f))
			{
				_universalCamData.enabled = true;

				// Debug.Log("start render and request ");
				if (_universalCamData.isActiveAndEnabled)
				{
					_camSensor.Render();

					var readbackRequest = AsyncGPUReadback.Request(_camSensor.targetTexture, 0, _readbackDstFormat, OnCompleteAsyncReadback);

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
			PushDeviceMessage<messages.ImageStamped>(_imageStamped);
		}

		protected virtual void ImageProcessing(ref NativeArray<byte> readbackData)
		{
			var image = _imageStamped.Image;
			_camImageData.SetTextureBufferData(readbackData);

			// Debug.Log(image.Data.Length);
			var imageData = _camImageData.GetImageData(image.Data.Length);
			if (imageData != null)
			{
				image.Data = imageData;
				if (_camParam.save_enabled && _startCameraWork)
				{
					var saveName = name + "_" + Time.time;
					_camImageData.SaveRawImageData(_camParam.save_path, saveName);
					// Debug.LogFormat("{0}|{1} captured", _camParam.save_path, saveName);
				}
			}
			else
			{
				Debug.LogWarningFormat("{0}: Failed to get image Data", name);
			}

			_imageStamped.Time.SetCurrentTime();
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return _sensorInfo;
		}

		public messages.Image GetImageDataMessage()
		{
			return (_imageStamped == null || _imageStamped.Image == null) ? null : _imageStamped.Image;
		}
	}
}