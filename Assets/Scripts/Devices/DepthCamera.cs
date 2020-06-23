/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using Unity.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class DepthCamera : Camera
	{
		// <noise> TBD
		// <lens> TBD
		// <distortion> TBD

		private Material depthMaterial = null;

		public bool isPointCloud = false;

		private DepthCamera()
			: base()
		{
		}

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

		protected override void OnStart()
		{
			SetupDepthCamera();
		}

		private void SetupDepthCamera()
		{
			// TODO : Need to be implemented!!!
			var depthShader = Shader.Find("Sensor/DepthCamera");
			// depthMaterial = new Material(depthShader);

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
		}
	}
}