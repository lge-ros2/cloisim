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
		private ComputeShader _computeShader;
		private int _kernelIndex;

		private Material depthMaterial = null;

		public uint depthScale = 1;

		public void ReverseDepthData(in bool reverse)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetFloat("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(_computeShader);

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_computeShader = Instantiate(Resources.Load<ComputeShader>("Shader/DepthBufferScaling"));
			_kernelIndex = _computeShader.FindKernel("CSDepthBufferScaling");

			var depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(depthShader);

			ReverseDepthData(true);

			var camParameters = (deviceParameters as SDF.Camera);

			if (camParameters.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				camParameters.image_format = "RGB_FLOAT32";
			}

			_cam.backgroundColor = Color.white;
			_cam.clearFlags = CameraClearFlags.SolidColor;

			_cam.depthTextureMode = DepthTextureMode.Depth;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			targetRTname = "CameraDepthTexture";
			targetRTdepth = 32;
			targetRTrwmode = RenderTextureReadWrite.Linear;
			targetRTformat = RenderTextureFormat.ARGB32;

			var pixelFormat = GetPixelFormat(camParameters.image_format);
			switch (pixelFormat)
			{
				case PixelFormat.L_INT16:
					readbackDstFormat = TextureFormat.R16;
					break;

				case PixelFormat.R_FLOAT16:
					readbackDstFormat = TextureFormat.RHalf;
					break;

				case PixelFormat.R_FLOAT32:
				default:
					readbackDstFormat = TextureFormat.RFloat;
					break;
			}

			var cb = new CommandBuffer();
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(BuiltinRenderTextureType.CameraTarget, tempTextureId);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			_cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);

			cb.ReleaseTemporaryRT(tempTextureId);
			cb.Release();
		}

		protected override void PostProcessing(ref byte[] buffer)
		{
			if (readbackDstFormat.Equals(TextureFormat.R16))
			{
				_computeShader.SetFloat("_DepthMin", (float)GetParameters().clip.near);
				_computeShader.SetFloat("_DepthMax", (float)GetParameters().clip.far);
				_computeShader.SetFloat("_DepthScale", (float)depthScale);

				var computeBuffer = new ComputeBuffer(buffer.Length, sizeof(byte));
				_computeShader.SetBuffer(_kernelIndex, "_Buffer", computeBuffer);
				computeBuffer.SetData(buffer);

				var threadGroupX = GetParameters().image_width/16;
				var threadGroupY = GetParameters().image_height/8;
				_computeShader.Dispatch(_kernelIndex, threadGroupX, threadGroupY, 1);
				computeBuffer.GetData(buffer);
				computeBuffer.Release();
			}
		}
	}
}