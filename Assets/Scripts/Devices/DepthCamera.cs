/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;

namespace SensorDevices
{
	public class DepthCamera : Camera
	{
		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader computeShader = null;
		private int kernelIndex = -1;

		private Material depthMaterial = null;

		public uint depthScale = 1;

		public void ReverseDepthData(in bool reverse)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		public void FlipXDepthData(in bool flip)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
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
				ComputeShaderDepthBuffer= Resources.Load<ComputeShader>("Shader/DepthBufferScaling");
			}

			computeShader = Instantiate(ComputeShaderDepthBuffer);
			kernelIndex = computeShader.FindKernel("CSDepthBufferScaling");

			var depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(depthShader);

			if (computeShader != null)
			{
				computeShader.SetFloat("_DepthMin", (float)camParameter.clip.near);
				computeShader.SetFloat("_DepthMax", (float)camParameter.clip.far);
				computeShader.SetFloat("_DepthScale", (float)depthScale);
			}

			ReverseDepthData(true);
			FlipXDepthData(false);

			if (camParameter.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				camParameter.image_format = "RGB_FLOAT32";
			}

			camSensor.backgroundColor = Color.white;
			camSensor.clearFlags = CameraClearFlags.SolidColor;

			camSensor.depthTextureMode = DepthTextureMode.Depth;
			universalCamData.requiresColorTexture = false;
			universalCamData.requiresDepthTexture = true;
			universalCamData.renderShadows = false;

			targetRTname = "CameraDepthTexture";
			targetRTdepth = 32;
			targetRTrwmode = RenderTextureReadWrite.Linear;
			targetRTformat = RenderTextureFormat.ARGB32;

			var pixelFormat = CameraData.GetPixelFormat(camParameter.image_format);
			switch (pixelFormat)
			{
				case CameraData.PixelFormat.L_INT16:
					readbackDstFormat = TextureFormat.R16;
					break;

				case CameraData.PixelFormat.R_FLOAT16:
					readbackDstFormat = TextureFormat.RHalf;
					break;

				case CameraData.PixelFormat.R_FLOAT32:
				default:
					readbackDstFormat = TextureFormat.RFloat;
					break;
			}

			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			camSensor.AddCommandBuffer(CameraEvent.AfterEverything, cb);

			cb.ReleaseTemporaryRT(tempTextureId);
			cb.Release();
		}

		protected override void PostProcessing(ref byte[] buffer)
		{
			if (readbackDstFormat.Equals(TextureFormat.R16) && computeShader != null)
			{
				var computeBuffer = new ComputeBuffer(buffer.Length, sizeof(byte));
				computeShader.SetBuffer(kernelIndex, "_Buffer", computeBuffer);
				computeBuffer.SetData(buffer);

				var threadGroupX = camParameter.image_width/16;
				var threadGroupY = camParameter.image_height/8;
				computeShader.Dispatch(kernelIndex, threadGroupX, threadGroupY, 1);
				computeBuffer.GetData(buffer);
				computeBuffer.Release();
			}
		}
	}
}