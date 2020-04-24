/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class MultiCamera : Device
	{
		private List<SensorDevices.Camera> cameras = new List<SensorDevices.Camera>();

		private gazebo.msgs.ImagesStamped imagesStamped;

		private Transform multiCamLink = null;

		public MultiCamera()
		{
			// Initialize Gazebo Message
			imagesStamped = new gazebo.msgs.ImagesStamped();
			imagesStamped.Time = new gazebo.msgs.Time();
		}

		protected override void OnStart()
		{
			foreach (var cam in cameras)
			{
				// cam = gameObject.AddComponent<UnityEngine.Camera>();
				// cam.transform.Rotate(Vector3.up, 90.0000000000f);
				// cameraLink = transform.parent;
				// InitializeMessages();
			}
			// if (cam)
			// {
			// 	SetupCamera();
			// 	StartCoroutine(CameraWorker());
			// }
		}

		public void AddCamera(in SDF.Camera parameters)
		{
			var newCamObject = new GameObject();
			newCamObject.name = "Camera::" + parameters.type;

			var newCamTransform = newCamObject.transform;
			newCamTransform.position = Vector3.zero;
			newCamTransform.rotation = Quaternion.identity;
			newCamTransform.localPosition = SDF2Unity.GetPosition(parameters.Pose.Pos);
			newCamTransform.localRotation = SDF2Unity.GetRotation(parameters.Pose.Rot);
			newCamTransform.SetParent(this.transform, false);

			var newCam = newCamObject.AddComponent<SensorDevices.Camera>();
			newCam.runningDeviceWork = false;
			newCam.deviceName = "MultiCamera::" + parameters.name;
			newCam.parameters = parameters;
			cameras.Add(newCam);
		}

		private void InitializeMessages()
		{
			// var pixelFormat = GetPixelFormat();
			// var depth = GetImageDepth();

			var image = imagesStamped.Images;
			// image.Width = (uint)parameters.image_width;
			// image.Height = (uint)parameters.image_height;
			// image.PixelFormat = (uint)pixelFormat;
			// image.Step = image.Width * depth;
			// image.Data = new byte[image.Height * image.Step];
		}

		private void SetupCamera()
		{
			// var depthShader = Shader.Find("Sensor/DepthShader");
			// depthMaterial = new Material(depthShader);

			// cam.ResetWorldToCameraMatrix();
			// cam.ResetProjectionMatrix();

			// cam.allowHDR = true;
			// cam.allowMSAA = false;
			// cam.targetDisplay = 0;
			// cam.stereoTargetEye = StereoTargetEyeMask.None;

			// cam.orthographic = false;
			// cam.nearClipPlane = (float)parameters.clip.near;
			// cam.farClipPlane = (float)parameters.clip.far;

			// var targetRT = new RenderTexture(parameters.image_width, parameters.image_height, 16, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			// targetRT.name = "CameraTexture";
			// cam.targetTexture = targetRT;

			// var camHFov = (float)parameters.horizontal_fov * Mathf.Rad2Deg;
			// var camVFov = DeviceHelper.HorizontalToVerticalFOV(camHFov, cam.aspect);
			// cam.fieldOfView = camVFov;

			// var projMatrix = DeviceHelper.MakeCustomProjectionMatrix(camHFov, camVFov, cam.nearClipPlane, cam.farClipPlane);

			// // // Invert for gazebo msg
			// var invertMatrix = Matrix4x4.Scale(new Vector3(	1, -1, 1));
			// cam.projectionMatrix = projMatrix * invertMatrix;
			// cam.enabled = false;
			// // cam.hideFlags |= HideFlags.NotEditable;

			// camData.AllocateTexture(parameters.image_width, parameters.image_height);
		}

		private IEnumerator CameraWorker()
		{
			while (true)
			{
				// var oldCulling = GL.invertCulling;
				// GL.invertCulling = !oldCulling;
				// cam.Render();
				// GL.invertCulling = oldCulling;
				// camData.SetTextureData(cam.targetTexture);

				// if (parameters.save_enabled)
				// {
				// 	var saveName = name + "_" + Time.time;
				// 	camData.SaveRawImageData(parameters.save_path, saveName);
				// 	// Debug.LogFormat("{0}|{1} captured", parameters.save_path, saveName);
				// }
				// cam.enabled = false;

				// imageData = camData.GetTextureData();

				// yield return new WaitForSeconds(UpdatePeriod * adjustCapturingRate);
			}
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();
			while (true)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var images = imagesStamped.Images;
			// if (image.Data.Length == imageData.Length)
			// {
			// 	image.Data = imageData;
			// }
			// // Debug.Log(imagesStamped.Image.Height + "," + imagesStamped.Image.Width);

			DeviceHelper.SetCurrentTime(imagesStamped.Time);
			PushData<gazebo.msgs.ImagesStamped>(imagesStamped);
		}
	}
}