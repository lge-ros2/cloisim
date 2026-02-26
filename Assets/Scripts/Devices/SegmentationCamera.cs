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

			// Set up HDRP Custom Pass for segmentation material override.
			// This replaces the URP SegmentationRenderObjects renderer feature
			// that is not available in HDRP. The custom pass overrides all
			// rendered objects' materials with the flat segmentation shader,
			// producing discrete class IDs without lighting or gradation.
			//
			// Uses a dedicated RenderTexture (not ctx.cameraColorBuffer) —
			// same pattern as DepthCapturePass — because HDRP internal buffers
			// have render state restrictions that prevent draw calls from
			// producing visible output even though clears work.
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

		/// <summary>
		/// Override readback to use the dedicated segmentation RT from the custom pass
		/// rather than the camera's target texture. Same pattern as DepthCamera.
		/// </summary>
		public override void ExecuteRender(float realtimeNow)
		{
			using (var marker = new Unity.Profiling.ProfilerMarker("SegmentationCamera.Render").Auto())
			{
				_lastCaptureRealtime = realtimeNow;

				_camSensor.Render();

				var segRT = _segmentationPass?.segmentationRT;
				if (segRT == null)
				{
					Debug.LogWarning($"{name}: SegmentationPass has no segmentationRT");
					return;
				}

				var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

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
				});
			}
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();
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