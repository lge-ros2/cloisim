/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class MultiCamera : Device
	{
		public List<SensorDevices.Camera> cameras = new List<SensorDevices.Camera>();

		private messages.ImagesStamped imagesStamped;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
		}

		protected override void OnStart()
		{
			var multiCamera = (deviceParameters as SDF.Cameras);
			foreach (var camParameters in multiCamera.cameras)
			{
				AddCamera(camParameters);
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
				for (var i = 0; i < cameras.Count; i++)
				{
					// Set images data only once
					var image = cameras[i].GetImageDataMessage();
					if (image != null)
					{
						imagesStamped.Images.Add(image);
					}
				}
			}

			DeviceHelper.SetCurrentTime(imagesStamped.Time);
			PushDeviceMessage<messages.ImagesStamped>(imagesStamped);
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
			newCam.Mode = ModeType.NONE;
			newCam.DeviceName = "MultiCamera::" + newCamObject.name ;
			newCam.SetDeviceParameter(parameters);

			cameras.Add(newCam);
		}

		public SensorDevices.Camera GetCamera(in string cameraName)
		{
			var target = "MultiCamera::" + cameraName;
			return cameras.Find(x => x.DeviceName.Equals(target));
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