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

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		private messages.Segmentation _segmentation = null;

		protected override void SetupTexture()
		{
			_targetRTname = "SegmentationTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			if (pixelFormat != CameraData.PixelFormat.L_INT16)
			{
				Debug.Log("Only support INT16 format");
			}

			// for Unsigned 16-bit
			_targetColorFormat = GraphicsFormat.R8G8_UNorm;
			_readbackDstFormat = GraphicsFormat.R8G8_UNorm;

			_camImageData = new CameraData.Image(_camParam.image.width, _camParam.image.height, pixelFormat);
		}

		protected override void SetupCamera()
		{
			// Debug.Log("Segmenataion Setup Camera");
			if (!_camParam.segmentation_type.Equals("semantic"))
			{
				Debug.Log("Only support semantic segmentation");
			}
			_camSensor.backgroundColor = Color.black;
			_camSensor.clearFlags = CameraClearFlags.SolidColor;
			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = true;

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
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();

			_segmentation = new messages.Segmentation();
			_segmentation.ImageStamped = _imageStamped;
		}

		protected override void GenerateMessage()
		{
			PushDeviceMessage<messages.Segmentation>(_segmentation);
		}

		protected override void ImageProcessing(ref NativeArray<byte> readbackData)
		{
			var image = _imageStamped.Image;
			_camImageData.SetTextureBufferData(readbackData);

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
				Debug.LogWarning($"{name}: Failed to get image Data");
			}

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

			_imageStamped.Time.SetCurrentTime();
		}
	}
}