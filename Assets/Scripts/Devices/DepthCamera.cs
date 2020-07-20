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

		public bool isPointCloud = false;

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
		protected override void SetupTexture()
		{
			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);
			depthMaterial.SetFloat("_ReverseData", 1.0f);

			if (parameters.depth_camera_output.Equals("points"))
			{
				isPointCloud = true;
				Debug.Log("Enable Point Cloud data mode");
				parameters.image_format = "RGB_FLOAT32";
			}
			else
			{
				parameters.image_format = "R_FLOAT32";
			}

			cam.backgroundColor = Color.white;
			cam.clearFlags = CameraClearFlags.SolidColor;
			cam.depthTextureMode = DepthTextureMode.Depth;

			targetRTname = "CameraDepthTexture";
			targetRTdepth = 24;
			targetRTrwmode = RenderTextureReadWrite.Linear;

			targetRTformat = RenderTextureFormat.ARGB32;
			readbackDstFormat = TextureFormat.RFloat;
		}
	}
}