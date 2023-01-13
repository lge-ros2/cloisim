/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace SensorDevices
{
	public class DepthCamera : Camera
	{
		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader computeShader = null;
		private int kernelIndex = -1;

		private Material depthMaterial = null;

		private uint depthScale = 1;

		public void ReverseDepthData(in bool reverse)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
			else
				Debug.Log("is null");
		}

		public void FlipXDepthData(in bool flip)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
			}
		}

		public void SetDepthScale(in uint value)
		{
			depthScale = value;

			if (computeShader != null)
			{
				computeShader.SetFloat("_DepthScale", (float)depthScale);
			}
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(computeShader);
			computeShader = null;
			Resources.UnloadAsset(ComputeShaderDepthBuffer);
			Resources.UnloadUnusedAssets();

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			if (ComputeShaderDepthBuffer == null)
			{
				ComputeShaderDepthBuffer = Resources.Load<ComputeShader>("Shader/DepthBufferScaling");
			}

			computeShader = Instantiate(ComputeShaderDepthBuffer);
			kernelIndex = computeShader.FindKernel("CSDepthBufferScaling");

			var depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(depthShader);

			if (computeShader != null)
			{
				computeShader.SetFloat("_DepthMax", (float)camParameter.clip.far);
			}

			ReverseDepthData(true);
			FlipXDepthData(false);

			if (camParameter.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				camParameter.image.format = "RGB_FLOAT32";
			}

			camSensor.backgroundColor = Color.white;
			camSensor.clearFlags = CameraClearFlags.Nothing;
			camSensor.renderingPath = RenderingPath.DeferredLighting;
			camSensor.depthTextureMode = DepthTextureMode.Depth;
			camSensor.allowHDR = false;
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			targetRTname = "CameraDepthTexture";
			targetColorFormat = GraphicsFormat.R8G8B8A8_UNorm;

			var pixelFormat = CameraData.GetPixelFormat(camParameter.image.format);
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT8:
					readbackDstFormat = GraphicsFormat.R8_UNorm;
					break;

				case CameraData.PixelFormat.L_INT16:
					readbackDstFormat = GraphicsFormat.R16_UNorm;
					break;

				case CameraData.PixelFormat.R_FLOAT16:
					readbackDstFormat = GraphicsFormat.R16_SFloat;
					break;

				case CameraData.PixelFormat.R_FLOAT32:
				default:
					Debug.Log("32bits depth format may cause application freezing.");
					readbackDstFormat = GraphicsFormat.R32_SFloat;
					break;
			}

			var cb = new CommandBuffer();
			cb.name = "CommandBufferForDepthShading";
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			cb.ReleaseTemporaryRT(tempTextureId);
			camSensor.AddCommandBuffer(CameraEvent.AfterEverything, cb);
			cb.Release();
		}

		private int threadGroupsX = 8;
		private int threadGroupsY = 8;

		protected override void PostProcessing(ref byte[] buffer)
		{
			if (depthScale > 1 && computeShader != null)
			{
				var computeBuffer = new ComputeBuffer(buffer.Length, sizeof(byte));
				computeShader.SetBuffer(kernelIndex, "_Buffer", computeBuffer);
				computeBuffer.SetData(buffer);

				var threadGroupX = camParameter.image.width / threadGroupsX;
				var threadGroupY = camParameter.image.height / threadGroupsY;
				computeShader.Dispatch(kernelIndex, threadGroupX, threadGroupY, 1);
				computeBuffer.GetData(buffer);
				computeBuffer.Release();
			}
		}
	}
}