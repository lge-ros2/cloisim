/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public class MultiCamera : Device
	{
		public List<SensorDevices.Camera> cameras = new List<SensorDevices.Camera>();

		private messages.ImagesStamped imagesStamped;

		public SDF.Cameras parameters = null;

		private Transform multiCamLink = null;

		protected override void OnAwake()
		{
		}

		protected override void OnStart()
		{
			foreach (var camParameters in parameters.list)
			{
				AddCamera(camParameters);
			}
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
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

		protected override void InitializeMessages()
		{
			imagesStamped = new messages.ImagesStamped();
			imagesStamped.Time = new messages.Time();
		}

		private void InitializeCamerasMessage()
		{
			foreach (var cam in cameras)
			{
				var image = cam.GetImageMessage();
				if (image == null)
				{
					break;
				}

				imagesStamped.Images.Add(image);
			}
		}

		protected override void GenerateMessage()
		{
			if (imagesStamped.Images.Count != cameras.Count)
			{
				InitializeCamerasMessage();
			}

			DeviceHelper.SetCurrentTime(imagesStamped.Time);
			PushData<messages.ImagesStamped>(imagesStamped);
		}

		private void AddCamera(in SDF.Camera parameters)
		{
			var newCamObject = new GameObject();
			newCamObject.name = parameters.name;

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

		public messages.CameraSensor GetCameraInfo(in string cameraName)
		{
			for (var index = 0; index < cameras.Count; index++)
			{
				if (cameras[index].deviceName.Equals(("MultiCamera::" + cameraName)))
				{
					return GetCameraInfo(index);
				}
			}

			return null;
		}

		public messages.CameraSensor GetCameraInfo(in int cameraIndex)
		{
			if (cameraIndex < cameras.Count)
			{
				return cameras[cameraIndex].GetCameraInfo();
			}
			else
			{
				Debug.LogWarning("unavailable camera index: " + cameraIndex);
				return null;
			}
		}
	}
}