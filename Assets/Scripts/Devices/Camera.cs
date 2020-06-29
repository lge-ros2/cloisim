/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class Camera : Device
	{
		protected gazebo.msgs.ImageStamped imageStamped = null;

		public SDF.Camera parameters = null;

		// TODO : Need to be implemented!!!
		// <noise> TBD
		// <lens> TBD
		// <distortion> TBD

		protected Transform cameraLink = null;

		protected UnityEngine.Camera cam = null;

		public float adjustCapturingRate = 1.5f;

		public bool runningDeviceWork = true;

		void Awake()
		{
			cam = gameObject.AddComponent<UnityEngine.Camera>();
			cameraLink = transform.parent;
		}

		protected override void OnStart()
		{
			if (cam)
			{
				cam.transform.Rotate(Vector3.up, 90.0000000000f);

				SetupCamera();
				StartCoroutine(CameraWorker());
			}
		}

		protected override void InitializeMessages()
		{
			imageStamped = new gazebo.msgs.ImageStamped();
			imageStamped.Time = new gazebo.msgs.Time();
			imageStamped.Image = new gazebo.msgs.Image();

			var image = imageStamped.Image;
			image.Width = (uint)parameters.image_width;
			image.Height = (uint)parameters.image_height;
			image.PixelFormat = (uint)GetPixelFormat(parameters.image_format);
			image.Step = image.Width * GetImageDepth(parameters.image_format);
			image.Data = new byte[image.Height * image.Step];
		}

		private void SetupCamera()
		{
			cam.ResetWorldToCameraMatrix();
			cam.ResetProjectionMatrix();

			cam.allowHDR = true;
			cam.allowMSAA = false;
			cam.targetDisplay = 0;
			cam.stereoTargetEye = StereoTargetEyeMask.None;

			cam.orthographic = false;
			cam.nearClipPlane = (float)parameters.clip.near;
			cam.farClipPlane = (float)parameters.clip.far;

			var targetRTname = "CameraTexture";
			var targetRTdepth = 0;
			var targetRTformat = RenderTextureFormat.ARGB32;
			var targetRTrwmode = RenderTextureReadWrite.sRGB;

			if (cam.depthTextureMode.Equals(DepthTextureMode.Depth))
			{
				targetRTname = "CameraDepthTexture";
				targetRTdepth = 24;
				targetRTformat = RenderTextureFormat.RFloat;
				targetRTrwmode = RenderTextureReadWrite.Linear;
			}

			var targetRT = new RenderTexture(parameters.image_width, parameters.image_height, targetRTdepth, targetRTformat, targetRTrwmode);
			targetRT.name = targetRTname;
			cam.targetTexture = targetRT;

			var camHFov = (float)parameters.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, cam.aspect);
			cam.fieldOfView = camVFov;

			// Invert projection matrix for gazebo msg
			var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, cam.nearClipPlane, cam.farClipPlane);
			var invertMatrix = Matrix4x4.Scale(new Vector3(1, -1, 1));
			cam.projectionMatrix = projMatrix * invertMatrix;
			cam.enabled = false;
			// cam.hideFlags |= HideFlags.NotEditable;

			camData.AllocateTexture(parameters.image_width, parameters.image_height, parameters.image_format);
		}

		private IEnumerator CameraWorker()
		{
			var waitForSeconds = new WaitForSeconds(UpdatePeriod * adjustCapturingRate);

			while (true)
			{
				var oldCulling = GL.invertCulling;
				GL.invertCulling = !oldCulling;
				cam.Render();
				GL.invertCulling = oldCulling;

				camData.SetTextureData(cam.targetTexture);

				if (parameters.save_enabled)
				{
					var saveName = name + "_" + Time.time;
					camData.SaveRawImageData(parameters.save_path, saveName);
					// Debug.LogFormat("{0}|{1} captured", parameters.save_path, saveName);
				}
				cam.enabled = false;

				yield return waitForSeconds;
			}
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();
			while (runningDeviceWork)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var image = imageStamped.Image;
			var imageData = camData.GetTextureData();
			if (image.Data.Length == imageData.Length)
			{
				image.Data = imageData;
			}
			// Debug.Log(imageStamped.Image.Height + "," + imageStamped.Image.Width);

			DeviceHelper.SetCurrentTime(imageStamped.Time);
			PushData<gazebo.msgs.ImageStamped>(imageStamped);
		}
	}
}