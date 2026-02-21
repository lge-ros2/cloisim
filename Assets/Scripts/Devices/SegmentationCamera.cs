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

			_textureForCapture = new Texture2D(_camParam.image.width, _camParam.image.height, TextureFormat.R16, false, true);
		}

		protected override void SetupCamera()
		{
			// Debug.Log("Segmenataion Setup");

			if (_hdCamData != null)
			{
				// HDRP segmentation camera: disable shadows and most effects
				_hdCamData.customRenderingSettings = true;
				var overrideMask = _hdCamData.renderingPathCustomFrameSettingsOverrideMask;
				overrideMask.mask[(uint)FrameSettingsField.ShadowMaps] = true;
				overrideMask.mask[(uint)FrameSettingsField.ContactShadows] = true;
				overrideMask.mask[(uint)FrameSettingsField.Volumetrics] = true;
				overrideMask.mask[(uint)FrameSettingsField.AtmosphericScattering] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSAO] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSR] = true;
				overrideMask.mask[(uint)FrameSettingsField.MotionVectors] = true;
				_hdCamData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;

				var frameSettings = _hdCamData.renderingPathCustomFrameSettings;
				frameSettings.SetEnabled(FrameSettingsField.ShadowMaps, false);
				frameSettings.SetEnabled(FrameSettingsField.ContactShadows, false);
				frameSettings.SetEnabled(FrameSettingsField.Volumetrics, false);
				frameSettings.SetEnabled(FrameSettingsField.AtmosphericScattering, false);
				frameSettings.SetEnabled(FrameSettingsField.SSAO, false);
				frameSettings.SetEnabled(FrameSettingsField.SSR, false);
				frameSettings.SetEnabled(FrameSettingsField.MotionVectors, false);
				_hdCamData.renderingPathCustomFrameSettings = frameSettings;
			}
// 			_hdCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
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

			if (image.Data != null && image.Data.Length == byteView.Length)
			{
				byteView.CopyTo(image.Data);
			}
			else
			{
				Debug.LogWarning($"{name}: Failed to get image Data. Size mismatch (Image: {image.Data?.Length}, Buffer: {byteView.Length})");
			}

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

			_messageQueue.Enqueue(segmentation);
		}
	}
}