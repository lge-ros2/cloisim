/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using messages = cloisim.msgs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		private SegmentationPass _segmentationPass;
		private Material _segMaterial;

		// ── Unified Ray Tracing ──
		// When Unified RT is available (hardware or compute backend), replaces
		// Camera.Render() + SegmentationPass with a single dispatch.
		// Instance IDs in the accel struct carry the segmentation class ID.
		private bool _useDXR = false;
		private UnityEngine.Rendering.UnifiedRayTracing.IRayTracingShader _urtShader;
		private GraphicsBuffer _urtScratchBuffer;
		private CommandBuffer _urtCmd;
		private RenderTexture _dxrOutputRT;

		public override bool IsURT => _useDXR;

		protected override void SetupTexture()
		{
			_targetRTname = "SegmentationTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			if (pixelFormat != CameraData.PixelFormat.L_INT16)
			{
				Debug.Log("Only support INT16 format");
			}

			// for Unsigned 16-bit
			_targetColorFormat = GraphicsFormat.R8G8B8A8_UNorm;
			_readbackDstFormat = GraphicsFormat.R8G8_UNorm;

			// Point filtering is critical for segmentation — bilinear filtering
			// would interpolate between class IDs at object edges, creating
			// gradation artifacts that corrupt the discrete label data.
			_rtFilterMode = FilterMode.Point;

			// Keep DepthBits.None (default) — the RT is a color buffer.
			// HDRP provides ctx.cameraDepthBuffer for depth testing in custom passes.

			_textureForCapture = new Texture2D(_camParam.image.width, _camParam.image.height, TextureFormat.R16, false, true);
			_textureForCapture.filterMode = FilterMode.Point;
		}

		protected override void SetupCamera()
		{
			if (_hdCamData != null)
			{
				// Segmentation camera only needs object IDs as colors — disable all
				// visual effects. TransparentObjects stays enabled so transparent
				// environment objects get segmented.
				var overrideMask = _hdCamData.renderingPathCustomFrameSettingsOverrideMask;
				overrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
				overrideMask.mask[(uint)FrameSettingsField.ShadowMaps] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSAO] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSR] = true;
				overrideMask.mask[(uint)FrameSettingsField.Volumetrics] = true;
				overrideMask.mask[(uint)FrameSettingsField.ReprojectionForVolumetrics] = true;
				overrideMask.mask[(uint)FrameSettingsField.AtmosphericScattering] = true;
				overrideMask.mask[(uint)FrameSettingsField.ContactShadows] = true;
				overrideMask.mask[(uint)FrameSettingsField.ScreenSpaceShadows] = true;
				overrideMask.mask[(uint)FrameSettingsField.SubsurfaceScattering] = true;
				overrideMask.mask[(uint)FrameSettingsField.Refraction] = true;
				overrideMask.mask[(uint)FrameSettingsField.Decals] = true;
				overrideMask.mask[(uint)FrameSettingsField.TransparentObjects] = true;
				_hdCamData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;

				var frameSettings = _hdCamData.renderingPathCustomFrameSettings;
				frameSettings.SetEnabled(FrameSettingsField.TransparentObjects, true);
				frameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
				frameSettings.SetEnabled(FrameSettingsField.ShadowMaps, false);
				frameSettings.SetEnabled(FrameSettingsField.SSAO, false);
				frameSettings.SetEnabled(FrameSettingsField.SSR, false);
				frameSettings.SetEnabled(FrameSettingsField.Volumetrics, false);
				frameSettings.SetEnabled(FrameSettingsField.ReprojectionForVolumetrics, false);
				frameSettings.SetEnabled(FrameSettingsField.AtmosphericScattering, false);
				frameSettings.SetEnabled(FrameSettingsField.ContactShadows, false);
				frameSettings.SetEnabled(FrameSettingsField.ScreenSpaceShadows, false);
				frameSettings.SetEnabled(FrameSettingsField.SubsurfaceScattering, false);
				frameSettings.SetEnabled(FrameSettingsField.Refraction, false);
				frameSettings.SetEnabled(FrameSettingsField.Decals, false);
				_hdCamData.renderingPathCustomFrameSettings = frameSettings;
			}

			// Try DXR ray tracing first
			InitDXRSegmentation();

			if (!_useDXR)
			{
				// Rasterization fallback: HDRP Custom Pass with material override
				_segMaterial = new Material(Shader.Find("Sensor/Segmentation"));

				var customPassVolume = gameObject.AddComponent<CustomPassVolume>();
				customPassVolume.targetCamera = _camSensor;
				customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterPostProcess;
				customPassVolume.isGlobal = false;

				_segmentationPass = customPassVolume.AddPassOfType<SegmentationPass>() as SegmentationPass;
				if (_segmentationPass == null)
				{
					Debug.LogError("SegmentationCamera: Failed to create SegmentationPass");
					return;
				}
				_segmentationPass.SetSegmentationMaterial(_segMaterial);
				_segmentationPass.SetLayerMask(_camSensor.cullingMask);
				_segmentationPass.SetTargetCamera(_camSensor);
			}
		}

		/// <summary>
		/// Initialize Unified Ray Tracing for segmentation camera if available.
		/// Instance IDs in the acceleration structure carry segmentation class IDs.
		/// </summary>
		private void InitDXRSegmentation()
		{
			var dxrManager = DXRSensorManager.Instance;
			if (dxrManager == null || !dxrManager.IsSupported) return;

			var shaderAsset = Resources.Load<ComputeShader>("Shader/URTSegmentationRaycast");
			if (shaderAsset == null) return;

			_urtShader = dxrManager.CreateShader(shaderAsset);
			if (_urtShader == null) return;

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			_urtCmd = new CommandBuffer { name = "SegmentationCameraURT" };

			// Pre-allocate scratch buffer
			var scratchSize = _urtShader.GetTraceScratchBufferRequiredSizeInBytes(width, height, 1);
			if (scratchSize > 0)
			{
				_urtScratchBuffer = new GraphicsBuffer(
					UnityEngine.Rendering.UnifiedRayTracing.RayTracingHelper.ScratchBufferTarget,
					(int)((scratchSize + 3) / 4), 4);
			}

			// Set static params
			var cmd = _urtCmd;
			cmd.Clear();
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_Width"), (int)width);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_Height"), (int)height);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_NearClip"), (float)_camParam.clip.near);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_FarClip"), (float)_camParam.clip.far);

			var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = SensorHelper.HorizontalToVerticalFOV(camHFov, (float)width / height);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfHFov"), Mathf.Tan(camHFov * 0.5f * Mathf.Deg2Rad));
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfVFov"), Mathf.Tan(camVFov * 0.5f * Mathf.Deg2Rad));
			Graphics.ExecuteCommandBuffer(cmd);

			_dxrOutputRT = new RenderTexture((int)width, (int)height, 0, GraphicsFormat.R8G8B8A8_UNorm)
			{
				name = "URTSegmentationOutput",
				filterMode = FilterMode.Point,
				enableRandomWrite = true,
			};
			_dxrOutputRT.Create();

			_useDXR = true;
			Debug.Log($"[SegmentationCamera:{DeviceName}] Unified RT enabled (backend: {dxrManager.RTContext.BackendType}) — {width}x{height}");
		}

		/// <summary>
		/// Override readback to use the dedicated segmentation RT from the custom pass
		/// rather than the camera's target texture. Same pattern as DepthCamera.
		/// </summary>
		public override void ExecuteRender(float realtimeNow)
		{
			using (var marker = new Unity.Profiling.ProfilerMarker("SegmentationCamera.Render").Auto())
			{
				AdvanceRenderSchedule(realtimeNow);

				if (_useDXR)
				{
					ExecuteRenderDXR(realtimeNow);
					return;
				}

				_camSensor.Render();

				var segRT = _segmentationPass?.segmentationRT;
				if (segRT == null)
				{
					Debug.LogWarning($"{name}: SegmentationPass has no segmentationRT");
					return;
				}

				var capturedTime = GetNextSyntheticTime();

				AsyncGPUReadback.Request(segRT, 0, _readbackDstFormat, (req) =>
				{
					if (req.hasError)
					{
						Debug.LogError($"{name}: Failed to read segmentation GPU texture (format={_readbackDstFormat})");
					}
					else if (req.done)
					{
						var readbackData = req.GetData<byte>();
						ImageProcessing<byte>(ref readbackData, capturedTime);
					}
					SignalDataReady();
				});
			}
		}

		/// <summary>
		/// Unified RT render path: single dispatch replaces
		/// Camera.Render() + SegmentationPass per-object DrawRenderer loop.
		/// </summary>
		private void ExecuteRenderDXR(float realtimeNow)
		{
			var dxrManager = DXRSensorManager.Instance;
			if (dxrManager?.AccelStruct == null) return;

			var capturedTime = GetNextSyntheticTime();
			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			var cmd = _urtCmd;
			cmd.Clear();

			var pos = _camSensor.transform.position;
			_urtShader.SetVectorParam(cmd, Shader.PropertyToID("_CameraOrigin"), new Vector4(pos.x, pos.y, pos.z, 0));
			_urtShader.SetMatrixParam(cmd, Shader.PropertyToID("_CameraToWorld"), _camSensor.transform.localToWorldMatrix);
			_urtShader.SetAccelerationStructure(cmd, "_AccelStruct", dxrManager.AccelStruct);
			_urtShader.SetTextureParam(cmd, Shader.PropertyToID("_OutputTex"), _dxrOutputRT);

			_urtShader.Dispatch(cmd, _urtScratchBuffer, width, height, 1);
			Graphics.ExecuteCommandBuffer(cmd);

			AsyncGPUReadback.Request(_dxrOutputRT, 0, _readbackDstFormat, (req) =>
			{
				if (req.hasError)
				{
					Debug.LogError($"{name}: URT segmentation readback failed");
				}
				else if (req.done)
				{
					var readbackData = req.GetData<byte>();
					ImageProcessing<byte>(ref readbackData, capturedTime);
				}
				SignalDataReady();
			});
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();
		}

		new void OnDestroy()
		{
			// Clean up URT resources
			_urtScratchBuffer?.Release();
			_urtScratchBuffer = null;
			_urtCmd?.Release();
			_urtCmd = null;
			_urtShader = null;

			if (_dxrOutputRT != null)
			{
				_dxrOutputRT.Release();
				_dxrOutputRT = null;
			}

			base.OnDestroy();
		}

		void LateUpdate()
		{
			if (_startCameraWork &&
				_camParam.save_enabled &&
				_messageQueue.TryPeek(out var msg))
			{
				var imageStampedMsg = ((messages.Segmentation)msg).ImageStamped;
				var saveName = $"{DeviceName}_{imageStampedMsg.Time.Sec}.{imageStampedMsg.Time.Nsec}";
				_textureForCapture.SaveRawImage(imageStampedMsg.Image.Data, _camParam.save_path, saveName);
			}
		}

		public System.Action<messages.Segmentation> OnSegmentationDataGenerated;

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			var segmentation = new messages.Segmentation();
			segmentation.ImageStamped = new messages.ImageStamped();
			segmentation.ImageStamped.Time = new messages.Time();
			segmentation.ImageStamped.Time.Set(capturedTime);

			segmentation.ImageStamped.Image = new messages.Image();
			segmentation.ImageStamped.Image = _image;

			var image = segmentation.ImageStamped.Image;
			var sizeOfT = UnsafeUtility.SizeOf<T>();
			var byteView = readbackData.Reinterpret<byte>(sizeOfT);

			CopyReadbackToImage(byteView, image.Data);

			// update labels
			var labelInfo = Main.SegmentationManager.GetLabelInfo();
			segmentation.ClassMaps.Clear();
			foreach (var kv in labelInfo)
			{
				if (kv.Value.Count > 0 && !kv.Value[0].Hide)
				{
					var visionClass = new messages.VisionClass()
					{
						ClassName = kv.Key,
						ClassId = kv.Value[0].ClassId
					};
					segmentation.ClassMaps.Add(visionClass);
				}
			}

			if (OnSegmentationDataGenerated != null) OnSegmentationDataGenerated.Invoke(segmentation);

			// Also invoke parent Camera events so CameraPlugin publishes the image natively
			if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(segmentation.ImageStamped);
			if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

			_messageQueue.Enqueue(segmentation);
		}
	}
}