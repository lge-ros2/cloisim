/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System.Collections.Concurrent;
using messages = cloisim.msgs;
using Unity.Collections;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		private new ConcurrentQueue<messages.Segmentation> _messageQueue = new ConcurrentQueue<messages.Segmentation>();

		protected override void OnReset()
		{
			_messageQueue.Clear();
			base.OnReset();
		}

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

			_camImageData = new CameraData.Image(_camParam.image.width, _camParam.image.height, pixelFormat);
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
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();
		}

		protected override void GenerateMessage()
		{
			while (_messageQueue.TryDequeue(out var msg))
			{
				PushDeviceMessage<messages.Segmentation>(msg);
			}
		}

		protected override void ImageProcessing(ref NativeArray<byte> readbackData, in float capturedTime)
		{
			var segmentation = new messages.Segmentation();
			segmentation.ImageStamped = new messages.ImageStamped();
			segmentation.ImageStamped.Time = new messages.Time();
			segmentation.ImageStamped.Time.Set(capturedTime);

			segmentation.ImageStamped.Image = new messages.Image();
			segmentation.ImageStamped.Image = _image;

			_camImageData.SetTextureBufferData(ref readbackData);

			var image = segmentation.ImageStamped.Image;
			var imageData = _camImageData.GetImageData(image.Data.Length);
			if (imageData != null)
			{
				image.Data = imageData;
				// Debug.LogFormat($"{image.Data[0]}|{image.Data[1]}|{image.Data[2]}|{image.Data[3]}");
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