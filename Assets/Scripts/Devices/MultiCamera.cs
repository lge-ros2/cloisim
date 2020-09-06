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

		protected override void GenerateMessage()
		{
			if (imagesStamped.Images.Count != cameras.Count)
			{
				foreach (var cam in cameras)
				{
					// Set images data only once
					var image = cam.GetImageDataMessage();
					if (image != null)
					{
						imagesStamped.Images.Add(image);
					}
				}
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
			newCam.SetDeviceParameter(parameters as SDF.SensorType);

			cameras.Add(newCam);
		}

		public SensorDevices.Camera GetCamera(in string cameraName)
		{
			var target = "MultiCamera::" + cameraName;
			return cameras.Find(x => x.deviceName.Equals(target));
		}

		public SensorDevices.Camera GetCamera(in int cameraIndex)
		{
			if (cameraIndex >= cameras.Count)
			{
				Debug.LogWarning("unavailable camera index: " + cameraIndex);
				return null;
			}

			return cameras[cameraIndex];
		}
	}
}