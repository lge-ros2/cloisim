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
		private List<SensorDevices.Camera> cameras = new List<SensorDevices.Camera>();

		private messages.ImagesStamped imagesStamped;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
		}

		protected override void OnStart()
		{
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

		public void AddCamera(in SensorDevices.Camera newCam)
		{
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