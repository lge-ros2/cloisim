/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Concurrent;
using Stopwatch = System.Diagnostics.Stopwatch;
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
		protected messages.Image _image = null; // for Parameters
		protected ConcurrentQueue<messages.ImageStamped> _messageQueue = new ConcurrentQueue<messages.ImageStamped>();

		// TODO : Need to be implemented!!!
		// <lens> TBD
		// <distortion> TBD

		protected UnityEngine.Camera _camSensor = null;
		protected UniversalAdditionalCameraData _universalCamData = null;

		protected string _targetRTname;
		protected GraphicsFormat _targetColorFormat;
		protected GraphicsFormat _readbackDstFormat;

		protected CameraData.Image _camImageData;
		private ConcurrentDictionary<int, AsyncWork.Camera> _asyncWorkList = new ConcurrentDictionary<int, AsyncWork.Camera>(); 

		public Noise noise = null;
		protected bool _startCameraWork = false;
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
				StartCoroutine(CameraWorker());
			}
		}

		protected override void OnReset()
		{
			while (_messageQueue.TryDequeue(out _)) { }

			_asyncWorkList.Clear();
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
			_image = new messages.Image();

			_sensorInfo = new messages.CameraSensor();
			_sensorInfo.ImageSize = new messages.Vector2d();
			_sensorInfo.Distortion = new messages.Distortion();
			_sensorInfo.Distortion.Center = new messages.Vector2d();
		}

		protected override void SetupMessages()
		{
			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			_image.Width = (uint)_camParam.image.width;
			_image.Height = (uint)_camParam.image.height;
			_image.PixelFormat = (uint)pixelFormat;
			_image.Step = _image.Width * (uint)CameraData.GetImageStep(pixelFormat);
			_image.Data = new byte[_image.Height * _image.Step];

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
			_camSensor.allowHDR = false;
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
				useMipMap: false,
				autoGenerateMips: false,
				isShadowMap: false,
				anisoLevel: 2,
				mipMapBias: 0,
				bindTextureMS: false,
				useDynamicScale: true,
				memoryless: RenderTextureMemoryless.None,
				name: _targetRTname);

			_camSensor.targetTexture = _rtHandle.rt;

			var isOrthoGraphic = false;
			if (_camParam.lens != null)
			{
				if (_camParam.lens.type.Equals("orthographic"))
				{
					isOrthoGraphic = true;
				}
			}

			if (isOrthoGraphic)
			{
				_camSensor.orthographic = true;
				_camSensor.orthographicSize = 5;
			}
			else
			{
				var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
				var camVFov = SensorHelper.HorizontalToVerticalFOV(camHFov, _camSensor.aspect);

				_camSensor.orthographic = false;
				_camSensor.fieldOfView = camVFov;

				var projMatrix = SensorHelper.MakeProjectionMatrixPerspective(camHFov, camVFov, _camSensor.nearClipPlane, _camSensor.farClipPlane);
				_camSensor.projectionMatrix = projMatrix;
			}

			// Invert projection matrix for cloisim msg
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			_camSensor.projectionMatrix *= invertMatrix;

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

		private IEnumerator CameraWorker()
		{
			var waitNextwork = new WaitForSeconds(WaitPeriod());

			while (_startCameraWork)
			{
				_universalCamData.enabled = true;

				// Debug.Log("start render and request ");
				if (_universalCamData.isActiveAndEnabled)
				{
					_camSensor.Render();

					var capturedTime = (float)DeviceHelper.GetGlobalClock().SimTime;
					var readbackRequest = AsyncGPUReadback.Request(_camSensor.targetTexture, 0, _readbackDstFormat, OnCompleteAsyncReadback);
					
					_asyncWorkList.TryAdd(readbackRequest.GetHashCode(), new AsyncWork.Camera(readbackRequest, capturedTime));
				}

				_universalCamData.enabled = false;

				yield return waitNextwork;
			}
		}

		protected void OnCompleteAsyncReadback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogError($"{name}: Failed to read GPU texture");
			}
			else if (request.done)
			{
				var sw = new Stopwatch();
				sw.Start();
				if (_asyncWorkList.TryRemove(request.GetHashCode(), out var asyncWork))
				{
					var readbackData = request.GetData<byte>();
					ImageProcessing(ref readbackData, asyncWork.capturedTime);
					readbackData.Dispose();
				}
				sw.Stop();
				if (sw.ElapsedMilliseconds > 0)
				Debug.Log(sw.ElapsedMilliseconds);	
			}
		}

		protected override void GenerateMessage()
		{
			while (_messageQueue.TryDequeue(out var msg))
			{
				PushDeviceMessage<messages.ImageStamped>(msg);
			}
		}

		protected virtual void ImageProcessing(ref NativeArray<byte> readbackData, in float capturedTime)
		{
			var imageStamped = new messages.ImageStamped();

			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);

			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			_camImageData.SetTextureBufferData(ref readbackData);

			var image = imageStamped.Image;
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

			_messageQueue.Enqueue(imageStamped);
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return _sensorInfo;
		}

		public messages.ImageStamped GetImageDataMessage()
		{
			if (_messageQueue.TryDequeue(out var msg))
			{
				return msg;
			}

			return null;
		}
	}
}