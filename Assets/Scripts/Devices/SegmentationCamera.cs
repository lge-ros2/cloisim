/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using messages = cloisim.msgs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		// Reusable protobuf objects to avoid per-frame GC allocations
		private messages.Segmentation _segmentation = null;

		protected override void SetupTexture()
		{
			_targetRTname = "SegmentationTexture";

			// Discrete label data must not be interpolated
			_rtFilterMode = FilterMode.Point;

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

			// Refer to SegmentationRenderer (Universal Renderer Data)
			_universalCamData.SetRenderer(1);
			_universalCamData.renderPostProcessing = true;
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = false;
			_universalCamData.dithering = true;
			_universalCamData.stopNaN = true;
			_universalCamData.allowHDROutput = false;
			_universalCamData.allowXRRendering = false;
			_universalCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

			// Prevent skybox from rendering; its depth values can leak
			// through the linearDepth threshold and produce a visible gradient.
			_camSensor.clearFlags = CameraClearFlags.SolidColor;
			_camSensor.backgroundColor = Color.black;
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();

			_segmentation = new messages.Segmentation();
			_segmentation.ImageStamped = _imageStamped;
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
			using (s_ImageProcessingMarker.Auto())
			{
				_timeMsg.Set(capturedTime);
				_imageStamped.Image = _image;

				var image = _imageStamped.Image;
				var sizeOfT = UnsafeUtility.SizeOf<T>();
				var byteView = readbackData.Reinterpret<byte>(sizeOfT);

				CopyReadbackToImage(byteView, image.Data);

				// update labels
				var labelInfo = Main.SegmentationManager.GetLabelInfo();
				_segmentation.ClassMaps.Clear();
				foreach (var kv in labelInfo)
				{
					if (kv.Value.Count > 0 && !kv.Value[0].Hide)
					{
						var visionClass = new messages.VisionClass()
						{
							ClassName = kv.Key,
							ClassId = kv.Value[0].ClassId
						};
						_segmentation.ClassMaps.Add(visionClass);
					}
				}

				EnqueueMessage(_segmentation);
			}
		}
	}
}