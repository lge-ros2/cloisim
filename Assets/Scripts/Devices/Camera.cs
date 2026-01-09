/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using messages = cloisim.msgs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class Camera : Device
	{
		protected SDF.Camera _camParam = null;
		protected messages.CameraSensor _sensorInfo = null;
		protected messages.Image _image = null; // for Parameters

		// TODO : Need to be implemented!!!
		// <lens> TBD
		// <distortion> TBD
		protected UnityEngine.Camera _camSensor = null;
		protected UniversalAdditionalCameraData _universalCamData = null;

		protected string _targetRTname;
		protected GraphicsFormat _targetColorFormat;
		protected GraphicsFormat _readbackDstFormat;

		protected bool _startCameraWork = false;
		private RTHandle _rtHandle;
		protected Texture2D _textureForCapture = null;

		private CommandBuffer _invertCullingOnCmdBuffer = null;
		private CommandBuffer _invertCullingOffCmdBuffer = null;
		private CommandBuffer _noiseCmdBuffer = null;
		private CommandBuffer _postProcessCmdBuffer = null;
		private Material _noiseMaterial = null;
		protected Material _depthMaterial = null;

		protected void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera == _camSensor)
			{
				context.ExecuteCommandBuffer(_invertCullingOnCmdBuffer);
				context.Submit();
			}
		}

		protected void OnEndCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
		{
			if (camera == _camSensor)
			{
				context.ExecuteCommandBuffer(_invertCullingOffCmdBuffer);

				if (_noiseMaterial != null || _depthMaterial != null)
				{
					int depthRT = Shader.PropertyToID("_TempDepthRT");
					int noiseRT = Shader.PropertyToID("_TempNoiseRT");
					_postProcessCmdBuffer.GetTemporaryRT(depthRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear);
					_postProcessCmdBuffer.GetTemporaryRT(noiseRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear);

					if (_depthMaterial != null)
					{
						_postProcessCmdBuffer.Blit(BuiltinRenderTextureType.CameraTarget, depthRT);

						if (_noiseMaterial != null)
						{
							_postProcessCmdBuffer.Blit(depthRT, noiseRT, _depthMaterial);
							_postProcessCmdBuffer.Blit(noiseRT, BuiltinRenderTextureType.CameraTarget, _noiseMaterial);
						}
						else
							_postProcessCmdBuffer.Blit(depthRT, BuiltinRenderTextureType.CameraTarget, _depthMaterial);
					}
					else
					{
						if (_noiseMaterial != null)
						{
							_postProcessCmdBuffer.Blit(BuiltinRenderTextureType.CameraTarget, noiseRT);
							_postProcessCmdBuffer.Blit(noiseRT, BuiltinRenderTextureType.CameraTarget, _noiseMaterial);
						}
					}

					_postProcessCmdBuffer.ReleaseTemporaryRT(depthRT);
					_postProcessCmdBuffer.ReleaseTemporaryRT(noiseRT);
					context.ExecuteCommandBuffer(_postProcessCmdBuffer);
					_postProcessCmdBuffer.Clear();
				}

				context.Submit();
			}
		}

		public void SetupNoise(in SDF.Noise param)
		{
			if (param != null)
			{
				Debug.Log($"{DeviceName}: Apply noise type:{param.type} mean:{param.mean} stddev:{param.stddev}");
				_noiseMaterial = new Material(Shader.Find("Sensor/Camera/GaussianNoise"));
				_noiseMaterial.SetFloat("_Mean", (float)param.mean);
				_noiseMaterial.SetFloat("_StdDev", (float)param.stddev);
				_noiseCmdBuffer = new CommandBuffer { name = "Gaussian Noise" };
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

		protected virtual void SetupTexture()
		{
			// Debug.Log("This is not a Depth Camera!");
			_targetRTname = "CameraColorTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			var textureFormatForCapture = TextureFormat.RGB24;
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT8:
					_targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					_readbackDstFormat = GraphicsFormat.R8_SRGB;
					textureFormatForCapture = TextureFormat.R8;
					break;

				case CameraData.PixelFormat.RGB_INT8:
				default:
					_targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
					_readbackDstFormat = GraphicsFormat.R8G8B8_SRGB;
					textureFormatForCapture = TextureFormat.RGB24;
					break;
			}

			_textureForCapture = new Texture2D(_camParam.image.width, _camParam.image.height, textureFormatForCapture, false, true);
			_textureForCapture.filterMode = FilterMode.Point;
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
			_invertCullingOnCmdBuffer = new CommandBuffer { name = "Invert Culling On" };
			_invertCullingOnCmdBuffer.SetInvertCulling(true);

			_invertCullingOffCmdBuffer = new CommandBuffer { name = "Invert Culling Off" };
			_invertCullingOffCmdBuffer.SetInvertCulling(false);

			_postProcessCmdBuffer = new CommandBuffer { name = "Depth + Noise or Noise PostProcess" };

			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
		}

		private void OnDisable()
		{
			RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
			RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

			_postProcessCmdBuffer?.Release();

			_invertCullingOnCmdBuffer?.Release();
			_invertCullingOffCmdBuffer?.Release();
		}

		private void SetupDefaultCamera()
		{
			_camSensor.ResetWorldToCameraMatrix();
			_camSensor.ResetProjectionMatrix();

			_camSensor.backgroundColor = Color.black;
			_camSensor.clearFlags = CameraClearFlags.Skybox;
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
		}

		protected new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Camera)");
			_startCameraWork = false;
			Thread.Sleep(1);
			StopAllCoroutines();

			_rtHandle?.Release();

			base.OnDestroy();
		}

		private IEnumerator CameraWorker()
		{
			var rateGap = (float)Application.targetFrameRate - UpdateRate;
			var messageGenerationTime = Mathf.Approximately(rateGap, 0) ? float.PositiveInfinity : 1f / rateGap;
			var waitNextCapture = new WaitForSeconds(WaitPeriod(messageGenerationTime));
			// Debug.Log($"CameraWorker {rateGap} {messageGenerationTime} {WaitPeriod(messageGenerationTime)}");
			while (_startCameraWork)
			{
				_universalCamData.enabled = true;

				// Debug.Log("start render and request ");
				if (_universalCamData.isActiveAndEnabled)
				{
					_camSensor.Render();

					var capturedTime = DeviceHelper.GetGlobalClock().SimTime;
					AsyncGPUReadback.Request(_camSensor.targetTexture, 0, _readbackDstFormat, (req) => {
						if (req.hasError)
						{
							Debug.LogError($"{name}: Failed to read GPU texture");
						}
						else if (req.done)
						{
							if (_depthMaterial == null)
							{
								var readbackData = req.GetData<byte>();
								ImageProcessing<byte>(ref readbackData, capturedTime);
							}
							else
							{
								var readbackData = req.GetData<float>();
								ImageProcessing<float>(ref readbackData, capturedTime);
							}
						}
					});
				}

				_universalCamData.enabled = false;

				yield return waitNextCapture;
			}
		}

		void LateUpdate()
		{
			if (_startCameraWork &&
				_textureForCapture != null &&
				_camParam.save_enabled &&
				_messageQueue.TryPeek(out var msg))
			{
				var imageStampedMsg = (messages.ImageStamped)msg;
				var saveName = $"{DeviceName}_{imageStampedMsg.Time.Sec}.{imageStampedMsg.Time.Nsec}";
				var format = CameraData.GetPixelFormat(_camParam.image.format);
				_textureForCapture.SaveRawImage(imageStampedMsg.Image.Data, _camParam.save_path, saveName, format);
			}
		}

		protected virtual void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			var imageStamped = new messages.ImageStamped();

			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);

			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			var image = imageStamped.Image;
			var sizeOfT = UnsafeUtility.SizeOf<T>();
			var byteView = readbackData.Reinterpret<byte>(sizeOfT);

			if (image.Data != null && image.Data.Length == byteView.Length)
			{
				byteView.CopyTo(image.Data);
			}
			else
			{
				Debug.LogWarning($"{name}: Failed to get image Data. Size mismatch (Image: {image.Data?.Length}, Buffer: {byteView.Length})");
			}

			_messageQueue.Enqueue(imageStamped);
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return _sensorInfo;
		}

		public global::ProtoBuf.IExtensible GetImageDataMessage()
		{
			if (_messageQueue.TryDequeue(out var msg))
			{
				return msg;
			}

			return null;
		}
	}
}