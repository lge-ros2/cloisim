/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		protected override void SetupTexture()
		{
			_targetRTname = "SegmentationTexture";

			var pixelFormat = CameraData.GetPixelFormat(camParameter.image.format);
			if (pixelFormat != CameraData.PixelFormat.L_INT16)
			{
				Debug.Log("Only support INT16 format");
			}

			_targetColorFormat = GraphicsFormat.R8G8B8A8_SRGB;
			_readbackDstFormat = GraphicsFormat.R8G8_UNorm; // for Unsigned 16-bit

			_camImageData = new CameraData.Image(camParameter.image.width, camParameter.image.height, pixelFormat);
		}

		protected override void SetupCamera()
		{
			if (!camParameter.segmentation_type.Equals("semantic"))
			{
				Debug.Log("Only support semantic segmentation");
			}

			camSensor.backgroundColor = Color.black;
			camSensor.clearFlags = CameraClearFlags.SolidColor;
			camSensor.allowHDR = false;
			camSensor.allowMSAA = false;

			// Refer to SegmentationRenderer (Universal Renderer Data)
			_universalCamData.SetRenderer(1);
			_universalCamData.renderPostProcessing = true;
			_universalCamData.requiresColorOption = CameraOverrideOption.On;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = true;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = false;
			_universalCamData.dithering = false;
		}

		protected override void ImageProcessing(ref NativeArray<byte> readbackData)
		{
			var image = imageStamped.Image;
			_camImageData.SetTextureBufferData(readbackData);

			var imageData = _camImageData.GetImageData(image.Data.Length);
			if (imageData != null)
			{
				image.Data = imageData;

				if (camParameter.save_enabled && _startCameraWork)
				{
					var saveName = name + "_" + Time.time;
					_camImageData.SaveRawImageData(camParameter.save_path, saveName);
					// Debug.LogFormat("{0}|{1} captured", camParameter.save_path, saveName);
				}
			}
			else
			{
				Debug.LogWarning($"{name}: Failed to get image Data");
			}

			DeviceHelper.SetCurrentTime(imageStamped.Time);
		}
	}
}