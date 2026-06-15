/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using messages = cloisim.msgs;
using Unity.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Material = UnityEngine.Material;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class Camera : Device, ISensorRenderable
	{
		#region Profiling markers
		private static readonly ProfilerMarker s_ExecuteRenderMarker = new("Camera.ExecuteRender");
		protected static readonly ProfilerMarker s_ImageProcessingMarker = new("Camera.ImageProcessing");
		private static readonly ProfilerMarker s_ConvertReadbackMarker = new("Camera.ConvertReadback");
		private static readonly ProfilerMarker s_CopyReadbackMarker = new("Camera.CopyReadback");
		#endregion

		protected SDFormat.CameraSensor _camParam = null;
		protected messages.CameraSensor _sensorInfo = null;
		protected messages.Image _image = null; // for Parameters

		// Reusable protobuf time object to avoid per-frame GC allocations
		protected messages.Time _timeMsg = null;

		// TODO : Need to be implemented!!!
		// <lens> TBD
		// <distortion> TBD
		protected UnityEngine.Camera _camSensor = null;
		protected UniversalAdditionalCameraData _universalCamData = null;

		/// <summary>Public accessor for sensor camera data.</summary>
		// public UniversalAdditionalCameraData UniversalCameraData => _universalCamData;

		protected string _targetRTname;
		protected GraphicsFormat _targetColorFormat;
		protected GraphicsFormat _readbackDstFormat;
		protected DepthBits _targetDepthBits = DepthBits.Depth24;

		/// <summary>
		/// Filter mode for the render target. Override to FilterMode.Point in
		/// subclasses (e.g. SegmentationCamera) where bilinear interpolation
		/// would corrupt discrete label data.
		/// </summary>
		protected FilterMode _rtFilterMode = FilterMode.Bilinear;

		protected bool _startCameraWork = false;
		private RenderTexture _renderTexture;

		/// <summary>
		/// When true, SetupDefaultCamera allocates a tiny 1×1 dummy render target
		/// instead of the full sensor resolution.
		/// </summary>
		protected bool _skipRTAllocation = false;
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
					// Use explicit target texture instead of BuiltinRenderTextureType.CameraTarget.
					// In some render pipelines, CameraTarget may reference an internal buffer
					// rather than camera.targetTexture, causing depth blits to go to the wrong RT.
					var cameraTarget = camera.targetTexture;

					int depthRT = Shader.PropertyToID("_TempDepthRT");
					int noiseRT = Shader.PropertyToID("_TempNoiseRT");
					_postProcessCmdBuffer.GetTemporaryRT(depthRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear);
					_postProcessCmdBuffer.GetTemporaryRT(noiseRT, camera.pixelWidth, camera.pixelHeight, 0, FilterMode.Bilinear);

					if (_depthMaterial != null)
					{
						_postProcessCmdBuffer.Blit(cameraTarget, depthRT);

						if (_noiseMaterial != null)
						{
							_postProcessCmdBuffer.Blit(depthRT, noiseRT, _depthMaterial);
							_postProcessCmdBuffer.Blit(noiseRT, cameraTarget, _noiseMaterial);
						}
						else
							_postProcessCmdBuffer.Blit(depthRT, cameraTarget, _depthMaterial);
					}
					else
					{
						if (_noiseMaterial != null)
						{
							_postProcessCmdBuffer.Blit(cameraTarget, noiseRT);
							_postProcessCmdBuffer.Blit(noiseRT, cameraTarget, _noiseMaterial);
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

		public void SetupNoise(in SDFormat.Noise param)
		{
			if (param != null && param.Type != SDFormat.NoiseType.None)
			{
				Debug.Log($"{DeviceName}: Apply noise type:{param.Type} mean:{param.Mean} stddev:{param.StdDev}");
				_noiseMaterial = new Material(Shader.Find("Sensor/Camera/GaussianNoise"))
				{
					hideFlags = HideFlags.DontUnloadUnusedAsset
				};
				_noiseMaterial.SetFloat("_Mean", (float)param.Mean);
				_noiseMaterial.SetFloat("_StdDev", (float)param.StdDev);
				_noiseCmdBuffer = new CommandBuffer { name = "Gaussian Noise" };
			}
		}

		public void SetParameter(in SDFormat.CameraSensor param)
		{
			_camParam = param;
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;

			_camSensor = GetComponent<UnityEngine.Camera>();
			_universalCamData = _camSensor.GetComponent<UniversalAdditionalCameraData>();
			if (_universalCamData == null)
			{
				_universalCamData = _camSensor.gameObject.AddComponent<UniversalAdditionalCameraData>();
			}

			// for controlling targetDisplay
			_camSensor.targetDisplay = -1;
			// These APIs are only available with the built-in renderer, not URP
			if (GraphicsSettings.currentRenderPipeline == null)
			{
				Debug.Log("Using built-in render pipeline settings for camera");
				_camSensor.renderingPath = RenderingPath.Forward;
				_camSensor.stereoTargetEye = StereoTargetEyeMask.None;
			}
		}

		protected override void OnStart()
		{
			if (_camSensor)
			{
				SetupTexture();
				CheckReadbackFormatSupport();
				SetupDefaultCamera();
				SetupCamera();
				_startCameraWork = true;

				// Register with centralized render manager.
				SensorRenderManager.Register(this, initialDelay: 0.1f);
			}
		}

		/// <summary>
		/// Force readback format to match the render target's native format when
		/// the requested format is not natively supported by the GPU for async
		/// readback (e.g. R8G8B8_SRGB on Vulkan). In that case Unity either
		/// performs an expensive internal conversion or returns data in the RT's
		/// native format, triggering a per-pixel CPU conversion in
		/// ConvertReadbackData() that can cost 60ms+ for HD resolutions.
		///
		/// By reading back as RGBA (matching the RT) and stripping the alpha
		/// channel afterwards, we avoid the GPU-side penalty entirely and keep
		/// the CPU conversion path — which is unavoidable either way — under
		/// our own control. The real fix is to store/publish RGBA and skip the
		/// strip altogether (see ImageProcessing).
		/// </summary>
		protected void CheckReadbackFormatSupport()
		{
			if (!SystemInfo.IsFormatSupported(_readbackDstFormat, GraphicsFormatUsage.ReadPixels))
			{
				Debug.LogWarning($"{DeviceName}: Readback format [{_readbackDstFormat}] not supported for ReadPixels, " +
					$"falling back to RT native format [{_targetColorFormat}] with CPU channel stripping");
				_readbackDstFormat = _targetColorFormat;
			}
		}

		protected virtual void SetupTexture()
		{
			// Debug.Log("This is not a Depth Camera!");
			_targetRTname = "CameraColorTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.ImageFormat);
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

			_textureForCapture = new Texture2D((int)_camParam.ImageWidth, (int)_camParam.ImageHeight, textureFormatForCapture, false, true)
			{
				filterMode = FilterMode.Point
			};
		}

		protected override void InitializeMessages()
		{
			_image = new messages.Image();
			_sensorInfo = new messages.CameraSensor
			{
				ImageSize = new messages.Vector2d(),
				Distortion = new messages.Distortion
				{
					Center = new messages.Vector2d()
				}
			};

			// Pre-allocate reusable protobuf objects for ImageProcessing
			_timeMsg = new messages.Time();
			_image.Header = new messages.Header
			{
				Stamp = _timeMsg
			};
		}

		protected override void SetupMessages()
		{
			var pixelFormat = CameraData.GetPixelFormat(_camParam.ImageFormat);
			_image.Width = _camParam.ImageWidth;
			_image.Height = _camParam.ImageHeight;
			_image.PixelFormatType = (messages.PixelFormatType)pixelFormat;
			_image.Step = _image.Width * (uint)CameraData.GetImageStep(pixelFormat);
			_image.Data = new byte[_image.Height * _image.Step];

			_sensorInfo.HorizontalFov = _camParam.HorizontalFov;
			_sensorInfo.ImageSize.X = _camParam.ImageWidth;
			_sensorInfo.ImageSize.Y = _camParam.ImageHeight;
			_sensorInfo.NearClip = _camParam.NearClip;
			_sensorInfo.FarClip = _camParam.FarClip;
			_sensorInfo.SaveEnabled = _camParam.SaveFrames;
			_sensorInfo.SavePath = _camParam.SavePath;

			if (_camParam.Element != null)
			{
				var distortionElement = _camParam.Element.FindElement("distortion");
				if (distortionElement != null)
				{
					var centerX = SDFormat.Extensions.GetElementValue(distortionElement, "center", "0 0").Split(' ');
					_sensorInfo.Distortion.Center.X = double.Parse(centerX[0]);
					_sensorInfo.Distortion.Center.Y = centerX.Length > 1 ? double.Parse(centerX[1]) : 0;
					_sensorInfo.Distortion.K1 = SDFormat.Extensions.GetElementValue(distortionElement, "k1", 0.0);
					_sensorInfo.Distortion.K2 = SDFormat.Extensions.GetElementValue(distortionElement, "k2", 0.0);
					_sensorInfo.Distortion.K3 = SDFormat.Extensions.GetElementValue(distortionElement, "k3", 0.0);
					_sensorInfo.Distortion.P1 = SDFormat.Extensions.GetElementValue(distortionElement, "p1", 0.0);
					_sensorInfo.Distortion.P2 = SDFormat.Extensions.GetElementValue(distortionElement, "p2", 0.0);
				}
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
			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = false;
			_camSensor.allowDynamicResolution = false;
			_camSensor.useOcclusionCulling = true;
			_camSensor.orthographic = false;
			_camSensor.nearClipPlane = (float)_camParam.NearClip;
			_camSensor.farClipPlane = (float)_camParam.FarClip;
			_camSensor.cullingMask = LayerMask.GetMask("Default", "Plane");

			// URT cameras skip full RT allocation to stay below the Vulkan driver's
			// concurrent render-target limit. Allocate a 1×1 dummy so CanRender
			// passes its targetTexture != null guard.
			var rtWidth = _skipRTAllocation ? 1 : (int)_camParam.ImageWidth;
			var rtHeight = _skipRTAllocation ? 1 : (int)_camParam.ImageHeight;

			// Create RenderTexture directly with explicit GraphicsFormat so that
			// graphicsFormat is properly reported to AsyncGPUReadback. RTHandles.Alloc
			// can produce textures whose graphicsFormat reads as None on some platforms,
			// causing async readback to fail.
			var desc = new RenderTextureDescriptor(rtWidth, rtHeight)
			{
				graphicsFormat = _targetColorFormat,
				depthStencilFormat = _targetDepthBits switch
				{
					DepthBits.Depth16 => GraphicsFormat.D16_UNorm,
					DepthBits.Depth24 => GraphicsFormat.D24_UNorm_S8_UInt,
					DepthBits.Depth32 => GraphicsFormat.D32_SFloat,
					_ => GraphicsFormat.None
				},
				msaaSamples = 1,
				dimension = TextureDimension.Tex2D,
				volumeDepth = 1,
				useMipMap = false,
				autoGenerateMips = false,
				enableRandomWrite = false,
				memoryless = RenderTextureMemoryless.None
			};

			_renderTexture = new RenderTexture(desc)
			{
				name = _targetRTname,
				filterMode = _rtFilterMode,
				wrapMode = TextureWrapMode.Clamp,
				anisoLevel = 0
			};
			_renderTexture.Create();

			if (_renderTexture.graphicsFormat == GraphicsFormat.None)
			{
				Debug.LogWarning($"{DeviceName}: RenderTexture graphicsFormat is None (requested {_targetColorFormat}), async readback may fail");
			}

			_camSensor.targetTexture = _renderTexture;

			var isOrthoGraphic = false;
			var lensElement = _camParam.Element?.FindElement("lens");
			if (lensElement != null)
			{
				var lensType = lensElement.FindElement("type")?.Value?.GetAsString() ?? string.Empty;
				if (lensType.Equals("orthographic"))
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
				var camHFov = (float)_camParam.HorizontalFov * Mathf.Rad2Deg;
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
			if (_universalCamData == null)
				return;

			_universalCamData.requiresColorOption = CameraOverrideOption.On;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = true;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = true;
			_universalCamData.antialiasing = AntialiasingMode.None;
			_universalCamData.enabled = false;
			_universalCamData.stopNaN = false;
			_universalCamData.dithering = false;
			_universalCamData.renderPostProcessing = false;
			_universalCamData.allowXRRendering = false;
			_universalCamData.volumeLayerMask = default;
			_universalCamData.renderType = CameraRenderType.Base;
			_universalCamData.cameraStack.Clear();
		}

		protected virtual void SetupCamera()
		{
			// Debug.Log("Base Setup Camera");
		}

		protected new void OnDestroy()
		{
			_startCameraWork = false;
			SensorRenderManager.Unregister(this);

			// Drain in-flight readbacks before releasing GPU resources
			AsyncGPUReadback.WaitAllRequests();

			if (_renderTexture != null)
			{
				_renderTexture.Release();
				Destroy(_renderTexture);
				_renderTexture = null;
			}

			base.OnDestroy();
		}

		#region BatchedRenderingInterface
		/// <summary>
		/// Override in subclasses that use URT. Default: false (full Camera.Render).
		/// </summary>
		public virtual bool IsURT => false;

		public float RenderPeriod => UpdatePeriod;

		/// <summary>
		/// Whether this camera is initialized and can accept render commands.
		/// </summary>
		public bool CanRender
		{
			get
			{
				if (!_startCameraWork) return false;
				if (_camSensor == null || _camSensor.targetTexture == null) return false;
				return true;
			}
		}

		/// <summary>
		/// ISensorRenderable implementation: one render step = one full camera render.
		/// Always returns true (single-step device).
		/// </summary>
		public bool ExecuteRenderStep(float realtimeNow)
		{
			ExecuteRender(realtimeNow);
			return true;
		}

		/// <summary>
		/// Execute a single render + async readback for this camera.
		/// Called by SensorRenderManager in a tight loop so the render
		/// pipeline can share state across sequential camera renders.
		/// </summary>
		protected virtual void ExecuteRender(float realtimeNow)
		{
			using (s_ExecuteRenderMarker.Auto())
			{
				_universalCamData.enabled = true;

				if (_universalCamData.isActiveAndEnabled)
				{
					// Capture actual sim time at render submission.
					// SensorRenderManager owns the schedule; the timestamp
					// must reflect when the scene was actually captured.
					var capturedTime = (Clock != null) ? Clock.SimTime : Time.timeAsDouble;

					_camSensor.Render();
					Device.GpuReadbackBegin();
					AsyncGPUReadback.Request(_camSensor.targetTexture, 0, _readbackDstFormat, (req) => {
						Device.GpuReadbackEnd();
						if (req.hasError)
						{
							Debug.LogError($"{name}: Failed to read GPU texture (format={_readbackDstFormat})");
						}
						else if (req.done)
						{
							if (_depthMaterial == null)
							{
								var readbackData = req.GetData<byte>();
								ImageProcessing(ref readbackData, capturedTime);
							}
							else
							{
								var readbackData = req.GetData<float>();
								ImageProcessing(ref readbackData, capturedTime);
							}
						}
					});
				}

				_universalCamData.enabled = false;
			}
		}
		#endregion // called by SensorRenderManager

		void LateUpdate()
		{
			if (_startCameraWork &&
				_textureForCapture != null &&
				_camParam.SaveFrames &&
				_messageQueue.TryPeek(out var msg))
			{
				var imageMsg = (messages.Image)msg;
				var saveName = $"{DeviceName}_{imageMsg.Header.Stamp.Sec}.{imageMsg.Header.Stamp.Nsec}";
				var format = CameraData.GetPixelFormat(_camParam.ImageFormat);

				if (format != CameraData.PixelFormat.L_INT8)
				{
					Debug.LogWarning($"{format.ToString()} is not support to save file");
					return;
				}
				_textureForCapture.SaveRawImage(imageMsg.Data, _camParam.SavePath, saveName);
			}
		}

		protected virtual void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			using (s_ImageProcessingMarker.Auto())
			{
				// Reuse preallocated protobuf objects instead of new per frame
				_timeMsg.Set(capturedTime);

				var sizeOfT = UnsafeUtility.SizeOf<T>();
				var byteView = readbackData.Reinterpret<byte>(sizeOfT);

				CopyReadbackToImage(byteView, _image.Data);

				EnqueueMessage(_image);
			}
		}

		/// <summary>
		/// Copy readback data to image buffer, converting format if sizes differ.
		/// Readback is always in the RT's native format (e.g. RGBA 4 BPP),
		/// while imageData is sized for the output format (e.g. RGB 3 BPP).
		/// </summary>
		protected void CopyReadbackToImage(NativeArray<byte> byteView, byte[] imageData)
		{
			using (s_CopyReadbackMarker.Auto())
			{
				if (imageData == null)
				{
					Debug.LogWarning($"{name}: image.Data is null");
				}
				else if (imageData.Length == byteView.Length)
				{
					byteView.CopyTo(imageData);
				}
				else if (byteView.Length > imageData.Length)
				{
					ConvertReadbackData(byteView, imageData);
				}
				else
				{
					Debug.LogWarning($"{name}: Failed to get image Data. Size mismatch (Image: {imageData.Length}, Buffer: {byteView.Length})");
				}
			}
		}

		/// <summary>
		/// Convert readback data from native format (e.g. RGBA 4 BPP) to desired format (e.g. RGB 3 BPP).
		/// Copies the first N channels from each source pixel group into the destination.
		/// Uses block processing for the RGBA→RGB hot path to improve cache and throughput.
		/// </summary>
		protected virtual void ConvertReadbackData(NativeArray<byte> src, byte[] dst)
		{
			using (s_ConvertReadbackMarker.Auto())
			{
				var pixelCount = (int)(_image.Width * _image.Height);
				if (pixelCount == 0) return;

				var srcBpp = src.Length / pixelCount;
				var dstBpp = dst.Length / pixelCount;

				unsafe
				{
					var srcPtr = (byte*)src.GetUnsafeReadOnlyPtr();
					fixed (byte* dstPtr = dst)
					{
						if (srcBpp == 4 && dstBpp == 3)
						{
							// Hot path: RGBA→RGB — process 4 pixels at a time
							// to reduce loop overhead and improve instruction throughput.
							var blockCount = pixelCount / 4;
							var remainder = pixelCount % 4;

							var s = srcPtr;
							var d = dstPtr;
							for (var b = 0; b < blockCount; b++)
							{
								// Pixel 0
								d[0] = s[0]; d[1] = s[1]; d[2] = s[2];
								// Pixel 1
								d[3] = s[4]; d[4] = s[5]; d[5] = s[6];
								// Pixel 2
								d[6] = s[8]; d[7] = s[9]; d[8] = s[10];
								// Pixel 3
								d[9] = s[12]; d[10] = s[13]; d[11] = s[14];

								s += 16; // 4 pixels * 4 BPP
								d += 12; // 4 pixels * 3 BPP
							}
							for (var i = 0; i < remainder; i++)
							{
								d[0] = s[0]; d[1] = s[1]; d[2] = s[2];
								s += 4;
								d += 3;
							}
						}
						else
						{
							// Generic path for other BPP combinations
							for (var i = 0; i < pixelCount; i++)
							{
								var si = i * srcBpp;
								var di = i * dstBpp;
								for (int c = 0; c < dstBpp; c++)
								{
									dstPtr[di + c] = srcPtr[si + c];
								}
							}
						}
					}
				}
			}
		}

		public messages.CameraSensor GetCameraInfo()
		{
			return _sensorInfo;
		}

		public ProtoBuf.IExtensible GetImageDataMessage()
		{
			if (_messageQueue.TryDequeue(out var msg))
			{
				return msg;
			}

			return null;
		}
	}
}