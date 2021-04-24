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
		private Material depthMaterial = null;

		public uint depthScale = 1;

		public void ReverseDepthData(in bool reverse)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetFloat("_ReverseData", (reverse)? 1.0f:0.0f);
			}
		}

		protected override void SetupTexture()
		{
			// var passId = new ShaderTagId("Sensor/Depth");

			// _DepthTexture.Init("_CameraDepthTexture");
			// _DepthPrepass = new DepthOnlyPass(RenderPassEvent.BeforeRenderingPrepasses, RenderQueueRange.opaque, 1);

			var _depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(_depthShader);

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
			cb.ReleaseTemporaryRT(tempTextureId);
			_cam.AddCommandBuffer(CameraEvent.AfterEverything, cb);
			cb.Release();
		}

		protected override void BufferDepthScaling(ref byte[] buffer)
		{
			if (readbackDstFormat.Equals(TextureFormat.R16))
			{
				// Debug.Log("sacling depth buffer");
				var depthMin = GetParameters().clip.near;
				var depthMax = GetParameters().clip.far;

 				for (var i = 0; i < buffer.Length; i += sizeof(ushort))
				{
					var depthDataInUInt16 = (ushort)buffer[i] << 8 | (ushort)buffer[i + 1];
					var depthDataRatio = (double)depthDataInUInt16 / (double)ushort.MaxValue;
					var scaledDepthData = (ushort)(depthDataRatio * depthMax * (double)depthScale);
					// Debug.Log( (ushort)buffer[i]<< 8 + "," + buffer[i+1] + "|" + depthDataInUInt16  + " => " + scaledDepthData);
					// restore scaled depth data
					buffer[i] = (byte)(scaledDepthData >> 8);
					buffer[i + 1] = (byte)(scaledDepthData);
				}
			}
		}
	}
}