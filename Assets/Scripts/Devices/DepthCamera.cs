/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SensorDevices
{
	public partial class DepthCamera : Camera
	{
		// <noise> TBD
		// <lens> TBD
		// <distortion> TBD

		private Material depthMaterial = null;

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (depthMaterial)
			{
				Graphics.Blit(source, destination, depthMaterial);
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		public void ReverseDepthData(in bool reverse)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetFloat("_ReverseData", (reverse)? 1.0f:0.0f);
			}
		}

		protected override void SetupTexture()
		{
			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);

			ReverseDepthData(true);

			var camParameters = (deviceParameters as SDF.Camera);

			if (camParameters.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				camParameters.image_format = "RGB_FLOAT32";
			}

			cam.backgroundColor = Color.white;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.depthTextureMode = DepthTextureMode.Depth;

			targetRTname = "CameraDepthTexture";
			targetRTdepth = 24;
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
		}
	}
}