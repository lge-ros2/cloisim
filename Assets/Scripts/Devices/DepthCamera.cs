/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using System;
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		#region "For Compute Shader"

		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader _computeShader = null;
		private int _kernelIndex = -1;
		private int _threadGroupX;
		private int _threadGroupY;
		ComputeBuffer _computeBufferSrc = null;
		ComputeBuffer _computeBufferDst = null;
		private byte[] _computedBufferOutput;
		#endregion

		private uint _depthScale = 1;

		public static void LoadComputeShader()
		{
			if (ComputeShaderDepthBuffer == null)
			{
				ComputeShaderDepthBuffer = Resources.Load<ComputeShader>("Shader/DepthBufferScaling");
			}
		}

		public static void UnloadComputeShader()
		{
			if (ComputeShaderDepthBuffer != null)
			{
				Resources.UnloadAsset(ComputeShaderDepthBuffer);
				Resources.UnloadUnusedAssets();
				ComputeShaderDepthBuffer = null;
			}
		}

		public void ReverseDepthData(in bool reverse)
		{
			if (_depthMaterial != null)
			{
				_depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		public void FlipXDepthData(in bool flip)
		{
			if (_depthMaterial != null)
			{
				_depthMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
			}
		}

		public void SetDepthScale(in uint value)
		{
			_depthScale = value;
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(_computeShader);
			_computeShader = null;

			_computeBufferSrc?.Release();
			_computeBufferDst?.Release();

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);
			var imageDepth = CameraData.GetImageDepth(format);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			_computeShader = Instantiate(ComputeShaderDepthBuffer);

			if (_computeShader != null)
			{
				_kernelIndex = _computeShader.FindKernel("CSScaleDepthBuffer");

				_computeShader.SetFloat("_DepthMax", (float)_camParam.clip.far);
				_computeShader.SetFloat("_DepthScale", (float)_depthScale);
				_computeShader.SetInt("_Width", width);
				_computeShader.SetInt("_Height", height);
				_computeShader.SetInt("_UnitSize", imageDepth);

				_computeShader.GetKernelThreadGroupSizes(_kernelIndex, out var threadX, out var threadY, out var _);

				// Consider packWidth, (8bit=4, 16bit=2, 32bit=1)
				var pack = (imageDepth == 4) ? 1 : (imageDepth == 2 ? 2 : 4);
				var packedWidth = Mathf.CeilToInt(width / (float)pack);

				_threadGroupX = Mathf.CeilToInt(packedWidth / (float)threadX);
				_threadGroupY = Mathf.CeilToInt(height / (float)threadY);
			}
			
			var pixelCount = width * height;
			var packedCount = (imageDepth == 4)
				? pixelCount
				: (imageDepth == 2 ? (pixelCount + 1) / 2 : (pixelCount + 3) / 4);

			_computeBufferSrc = new ComputeBuffer(pixelCount, sizeof(float));
			_computeBufferDst = new ComputeBuffer(packedCount, sizeof(uint));
			_computedBufferOutput = new byte[packedCount * sizeof(uint)];
		}

		protected override void SetupCamera()
		{
			// Debug.Log("Depth Setup Camera");
			var depthShader = Shader.Find("Sensor/DepthRange");
			_depthMaterial = new Material(depthShader);

			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = false;
			_camSensor.depthTextureMode = DepthTextureMode.Depth;

			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			ReverseDepthData(false);
			FlipXDepthData(false);
		}

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			var imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);

			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			if (_computeShader != null)
			{
				_computeShader.SetBuffer(_kernelIndex, "_Input", _computeBufferSrc);
				_computeBufferSrc.SetData(readbackData);

				_computeShader.SetBuffer(_kernelIndex, "_Output", _computeBufferDst);
				_computeShader.Dispatch(_kernelIndex, _threadGroupX, _threadGroupY, 1);
				_computeBufferDst.GetData(_computedBufferOutput);

        		Buffer.BlockCopy(_computedBufferOutput, 0, imageStamped.Image.Data, 0, imageStamped.Image.Data.Length);
			}

			_messageQueue.Enqueue(imageStamped);
		}
	}
}