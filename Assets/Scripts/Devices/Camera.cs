/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using messages = cloisim.msgs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class Camera : Device, ISensorRenderable
	{
		// ── Profiling markers ──
		private static readonly ProfilerMarker s_ExecuteRenderMarker = new("Camera.ExecuteRender");
		private static readonly ProfilerMarker s_HdrpRenderMarker = new("Camera.HdrpRender");
		private static readonly ProfilerMarker s_ImageProcessingMarker = new("Camera.ImageProcessing");
		private static readonly ProfilerMarker s_ConvertReadbackMarker = new("Camera.ConvertReadback");
		private static readonly ProfilerMarker s_CopyReadbackMarker = new("Camera.CopyReadback");

		protected SDF.Camera _camParam = null;
		protected messages.CameraSensor _sensorInfo = null;
		protected messages.Image _image = null; // for Parameters

		// Reusable protobuf objects to avoid per-frame GC allocations
		private messages.ImageStamped _imageStamped = null;
		private messages.Time _timeMsg = null;

		// TODO : Need to be implemented!!!
		// <lens> TBD
		// <distortion> TBD
		protected UnityEngine.Camera _camSensor = null;
		protected HDAdditionalCameraData _hdCamData = null;

		// ── Batched rendering: managed by SensorRenderManager ──
		// Instead of each camera running its own coroutine, a central
		// manager renders all cameras in a tight batch each frame.
		// This reduces HDRP per-camera CPU overhead by allowing the
		// render pipeline to share state across sequential renders.
		protected float _lastCaptureRealtime = 0f;
		private float _initTime = 0f;

		protected string _targetRTname;
		protected GraphicsFormat _targetColorFormat;
		protected GraphicsFormat _readbackDstFormat;
		protected DepthBits _targetDepthBits = DepthBits.None;
		/// <summary>
		/// Filter mode for the render target. Override to FilterMode.Point in
		/// subclasses (e.g. SegmentationCamera) where bilinear interpolation
		/// would corrupt discrete label data.
		/// </summary>
		protected FilterMode _rtFilterMode = FilterMode.Bilinear;

		protected bool _startCameraWork = false;
		protected bool _needsReadbackFormatConversion = false;
		private RTHandle _rtHandle;
		protected Texture2D _textureForCapture = null;

		private CommandBuffer _invertCullingOnCmdBuffer = null;
		private CommandBuffer _invertCullingOffCmdBuffer = null;
		private CommandBuffer _noiseCmdBuffer = null;
		private CommandBuffer _postProcessCmdBuffer = null;
		private Material _noiseMaterial = null;
		protected Material _depthMaterial = null;

		// Lightweight tonemap: renders to HDR float16 target, then a single
		// fullscreen blit through ACES tonemap shader produces LDR output.
		// Much cheaper than HDRP Postprocess (~0.1ms vs ~5ms per camera).
		private RTHandle _ldrReadbackRT;
		private Material _tonemapMaterial;

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
					// In HDRP Render Graph, CameraTarget may reference an internal buffer
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

		public void SetupNoise(in SDF.Noise param)
		{
			if (param != null)
			{
				Debug.Log($"{DeviceName}: Apply noise type:{param.type} mean:{param.mean} stddev:{param.stddev}");
				_noiseMaterial = new Material(Shader.Find("Sensor/Camera/GaussianNoise"));
				_noiseMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
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
			_hdCamData = _camSensor.GetComponent<HDAdditionalCameraData>();
			if (_hdCamData == null)
			{
				_hdCamData = _camSensor.gameObject.AddComponent<HDAdditionalCameraData>();
			}

			// for controlling targetDisplay
			_camSensor.targetDisplay = -1;
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
			{
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
				// The manager runs a single coroutine (not from WaitForEndOfFrame
				// context) and renders all cameras in a tight batch each frame.
				// This replaces per-camera CameraWorker coroutines and avoids
				// the Unity 6000 coroutine context bug.
				_initTime = Time.realtimeSinceStartup;
				_lastCaptureRealtime = _initTime;
				SensorRenderManager.Instance.Register(this);
			}
		}

		/// <summary>
		/// Verify the desired readback format is supported for GPU readback.
		/// On Vulkan/Linux, 3-byte formats like R8G8B8_SRGB are often unsupported.
		/// Falls back to the render target's native format with CPU-side conversion.
		/// </summary>
		protected void CheckReadbackFormatSupport()
		{
			// Readback always comes from _ldrReadbackRT (R8G8B8A8_SRGB) when it exists,
			// not from the HDR camera target.
			var readbackSourceFormat = (_ldrReadbackRT != null)
				? GraphicsFormat.R8G8B8A8_SRGB
				: _targetColorFormat;

			if (_readbackDstFormat != readbackSourceFormat)
			{
				if (!SystemInfo.IsFormatSupported(_readbackDstFormat, FormatUsage.ReadPixels))
				{
					Debug.LogWarning($"{DeviceName}: Readback format {_readbackDstFormat} not supported for ReadPixels, falling back to {readbackSourceFormat} with CPU conversion");
					_readbackDstFormat = readbackSourceFormat;
					_needsReadbackFormatConversion = true;
				}
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
					// HDR format for HDRP internal rendering — matches GUI camera behavior.
					// HDRP Postprocess (auto-exposure, ACES tonemapping) renders to this HDR target,
					// then we blit HDR→LDR for readback.
					_targetColorFormat = GraphicsFormat.R16G16B16A16_SFloat;
					_readbackDstFormat = GraphicsFormat.R8G8B8_SRGB;
					textureFormatForCapture = TextureFormat.RGB24;
					break;
			}

			_textureForCapture = new Texture2D(_camParam.image.width, _camParam.image.height, textureFormatForCapture, false, true);
			_textureForCapture.filterMode = FilterMode.Point;

			// HDRP Postprocess handles tonemapping/exposure/color grading (matching GUI rendering).
			// No custom tonemap shader needed — HDR→LDR blit uses hardware sRGB conversion.
			_tonemapMaterial = null;
		}

		protected override void InitializeMessages()
		{
			_image = new messages.Image();
			_sensorInfo = new messages.CameraSensor();
			_sensorInfo.ImageSize = new messages.Vector2d();
			_sensorInfo.Distortion = new messages.Distortion();
			_sensorInfo.Distortion.Center = new messages.Vector2d();

			// Pre-allocate reusable protobuf objects for ImageProcessing
			_timeMsg = new messages.Time();
			_imageStamped = new messages.ImageStamped();
			_imageStamped.Time = _timeMsg;
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
			// These APIs are only available with the built-in renderer, not HDRP/URP
			if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
			{
				_camSensor.renderingPath = RenderingPath.Forward;
				_camSensor.stereoTargetEye = StereoTargetEyeMask.None;
			}
			// Allow HDR for HDR render targets (RGB cameras) — HDRP Postprocess needs
			// HDR rendering for proper auto-exposure and tonemapping.
			// Non-HDR targets (Depth/Segmentation/L_INT8) keep allowHDR=false.
			var isHdrTarget = (_targetColorFormat == GraphicsFormat.R16G16B16A16_SFloat);
			_camSensor.allowHDR = isHdrTarget;
			// Sensor cameras: disable MSAA — robot perception doesn't need anti-aliasing,
			// and MSAA adds GPU overhead per render target resolve.
			_camSensor.allowMSAA = false;
			// Disable dynamic resolution — sensor images must be exact pixel dimensions.
			_camSensor.allowDynamicResolution = false;
			_camSensor.useOcclusionCulling = true;
			_camSensor.orthographic = false;
			_camSensor.nearClipPlane = (float)_camParam.clip.near;
			_camSensor.farClipPlane = (float)_camParam.clip.far;
			_camSensor.cullingMask = LayerMask.GetMask("Default", "Plane");

			RTHandles.SetHardwareDynamicResolutionState(false);
			_rtHandle = RTHandles.Alloc(
				width: _camParam.image.width,
				height: _camParam.image.height,
				slices: 1,
				depthBufferBits: _targetDepthBits,
				colorFormat: _targetColorFormat,
				filterMode: _rtFilterMode,
				wrapMode: TextureWrapMode.Clamp,
				dimension: TextureDimension.Tex2D,
				// No MSAA — sensor images go directly to robot perception,
				// no need for anti-aliasing. Saves GPU resolve pass per camera.
				msaaSamples: MSAASamples.None,
				enableRandomWrite: false,
				useMipMap: false,
				autoGenerateMips: false,
				isShadowMap: false,
				anisoLevel: 0,
				mipMapBias: 0,
				bindTextureMS: false,
				useDynamicScale: false,
				memoryless: RenderTextureMemoryless.None,
				name: _targetRTname);

			_camSensor.targetTexture = _rtHandle.rt;

			// Create LDR readback target for HDR cameras — needed to convert HDR→LDR for readback.
			// When using custom tonemap: blit through tonemap shader.
			// When using HDRP Postprocess: pass-through blit with hardware sRGB conversion.
			// Non-HDR cameras (Depth/Segmentation) skip this — they readback directly.
			if (isHdrTarget)
			{
				_ldrReadbackRT = RTHandles.Alloc(
					width: _camParam.image.width,
					height: _camParam.image.height,
					slices: 1,
					depthBufferBits: DepthBits.None,
					colorFormat: GraphicsFormat.R8G8B8A8_SRGB,
					filterMode: FilterMode.Bilinear,
					wrapMode: TextureWrapMode.Clamp,
					dimension: TextureDimension.Tex2D,
					msaaSamples: MSAASamples.None,
					enableRandomWrite: false,
					useMipMap: false,
					autoGenerateMips: false,
					name: "CameraLDRReadback");
			}

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

			SetDefaultHDAdditionalCameraData();

			_camSensor.enabled = false;
			// _camSensor.hideFlags |= HideFlags.NotEditable;
		}

		private void SetDefaultHDAdditionalCameraData()
		{
			if (_hdCamData == null)
				return;

			// RGB sensor cameras: match GUI rendering quality exactly.
			// Keep ALL HDRP features at their defaults including Postprocess
			// (tonemapping, exposure, color grading from the Volume system).
			// Only disable MotionVectors (unnecessary for sensor capture).
			_hdCamData.customRenderingSettings = true;

			var overrideMask = _hdCamData.renderingPathCustomFrameSettingsOverrideMask;
			overrideMask.mask[(uint)FrameSettingsField.MotionVectors] = true;
			_hdCamData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;

			var frameSettings = _hdCamData.renderingPathCustomFrameSettings;
			frameSettings.SetEnabled(FrameSettingsField.MotionVectors, false);
			_hdCamData.renderingPathCustomFrameSettings = frameSettings;
		}

		protected virtual void SetupCamera()
		{
			// Debug.Log("Base Setup Camera");
		}

		protected new void OnDestroy()
		{
			_startCameraWork = false;
			SensorRenderManager.Instance.Unregister(this);

			_rtHandle?.Release();
			_ldrReadbackRT?.Release();

			base.OnDestroy();
		}

		// ═══════════════════════════════════════════════════════════════
		//  Batched rendering interface — called by SensorRenderManager
		// ═══════════════════════════════════════════════════════════════

		/// <summary>
		/// Check if this camera should render this frame.
		/// Uses real-time rate limiting (not Unity Time) to prevent the
		/// death spiral where all cameras render every frame when FPS
		/// drops below the target update rate.
		/// </summary>
		public bool IsReadyToRender(float realtimeNow)
		{
			if (!_startCameraWork) return false;
			if (_camSensor == null || _camSensor.targetTexture == null) return false;

			// Allow HDRP 100ms to initialize HDCamera state after setup
			if (realtimeNow - _initTime < 0.1f) return false;

			return (realtimeNow - _lastCaptureRealtime) >= UpdatePeriod;
		}

		/// <summary>
		/// How overdue this camera is for rendering (seconds past its
		/// update period). Used by SensorRenderManager to prioritize
		/// the most starved cameras when frame budget is limited.
		/// </summary>
		public float GetRenderUrgency(float realtimeNow)
		{
			return (realtimeNow - _lastCaptureRealtime) - UpdatePeriod;
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
		/// Called by SensorRenderManager in a tight loop (no yields
		/// between cameras) so HDRP can share shadow atlas and render
		/// graph state across sequential camera renders.
		/// </summary>
		public virtual void ExecuteRender(float realtimeNow)
		{
			using (s_ExecuteRenderMarker.Auto())
			{
				_lastCaptureRealtime = realtimeNow;

				using (s_HdrpRenderMarker.Auto())
				{
					_camSensor.Render();
				}

				// Lightweight tonemap: blit HDR render target through ACES shader
				// to LDR readback target. Single fullscreen pass, ~0.1ms on GPU.
				var readbackTarget = _camSensor.targetTexture;
				if (_ldrReadbackRT != null)
				{
					// Blit HDR camera target → LDR readback target.
					// Custom tonemap: ACES shader. HDRP Postprocess: pass-through (hardware sRGB).
					if (_tonemapMaterial != null)
						Graphics.Blit(_camSensor.targetTexture, _ldrReadbackRT.rt, _tonemapMaterial);
					else
						Graphics.Blit(_camSensor.targetTexture, _ldrReadbackRT.rt);
					readbackTarget = _ldrReadbackRT.rt;
				}

				var capturedTime = DeviceHelper.GetGlobalClock().SimTime;
				AsyncGPUReadback.Request(readbackTarget, 0, _readbackDstFormat, (req) => {
					if (req.hasError)
					{
						Debug.LogError($"{name}: Failed to read GPU texture (format={_readbackDstFormat})");
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

				if (format != SensorDevices.CameraData.PixelFormat.L_INT8)
				{
					Debug.LogWarning($"{format.ToString()} is not support to save file");
					return;
				}
				_textureForCapture.SaveRawImage(imageStampedMsg.Image.Data, _camParam.save_path, saveName);
			}
		}

		public System.Action<messages.ImageStamped> OnCameraDataGenerated;
		public System.Action<messages.CameraSensor> OnCameraInfoGenerated;

		protected virtual void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			using (s_ImageProcessingMarker.Auto())
			{
				// Reuse preallocated protobuf objects instead of new per frame
				_timeMsg.Set(capturedTime);
				_imageStamped.Image = _image;

				var image = _imageStamped.Image;
				var sizeOfT = UnsafeUtility.SizeOf<T>();
				var byteView = readbackData.Reinterpret<byte>(sizeOfT);

				CopyReadbackToImage(byteView, image.Data);

				if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(_imageStamped);
				if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

				_messageQueue.Enqueue(_imageStamped);
			}
		}

		/// <summary>
		/// Copy readback data to image buffer, handling format conversion if needed.
		/// When the GPU doesn't support the desired readback format (e.g. R8G8B8_SRGB on Vulkan),
		/// we read back in the render target's native format (RGBA) and strip extra channels on CPU.
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
				else if (_needsReadbackFormatConversion)
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
		/// </summary>
		protected virtual void ConvertReadbackData(NativeArray<byte> src, byte[] dst)
		{
			using (s_ConvertReadbackMarker.Auto())
			{
				var pixelCount = (int)(_image.Width * _image.Height);
				if (pixelCount == 0) return;

				var srcBpp = src.Length / pixelCount;
				var dstBpp = dst.Length / pixelCount;

				// Fast RGBA→RGB stripping using unsafe pointer arithmetic.
				// Unrolled 3-byte copy avoids inner loop overhead (~2x faster than loop).
				unsafe
				{
					var srcPtr = (byte*)src.GetUnsafeReadOnlyPtr();
					fixed (byte* dstPtr = dst)
					{
						if (srcBpp == 4 && dstBpp == 3)
						{
							// Hot path: RGBA→RGB (most common case for sensor cameras)
							for (int i = 0; i < pixelCount; i++)
							{
								var s = srcPtr + i * 4;
								var d = dstPtr + i * 3;
								d[0] = s[0];
								d[1] = s[1];
								d[2] = s[2];
							}
						}
						else
						{
							// Generic path for other BPP combinations
							for (int i = 0; i < pixelCount; i++)
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