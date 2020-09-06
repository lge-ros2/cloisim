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

		protected override void SetupTexture()
		{
			var shader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(shader);
			depthMaterial.SetFloat("_ReverseData", 1.0f);

			var camParameters = (deviceParameters as SDF.Camera);
			switch (camParameters.depth_camera_output)
			{
				case "points":
					Debug.Log("Enable Point Cloud data mode");
					camParameters.image_format = "RGB_FLOAT32";
					break;

				default:
					camParameters.image_format = "R_FLOAT32";
					break;
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