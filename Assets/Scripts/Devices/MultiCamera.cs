/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// #define USE_AVERAGE_TIME_FOR_IMAGE_SYNC

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

		protected override void SetupMessages()
		{
			for (var i = 0; i < cameras.Count; i++)
			{
				imagesStamped.Images.Add(new messages.Image());
			}
		}

		protected override void GenerateMessage()
		{
#if USE_AVERAGE_TIME_FOR_IMAGE_SYNC
			float imageStampTimeSum = 0;
			int imageStampedCount = 0;
#else
			float imagesStampedTime = 0;
#endif
			for (var i = 0; i < cameras.Count; i++)
			{
				// Set images data only once
				var imageStamped = cameras[i].GetImageDataMessage();
				if (imageStamped != null && i < imagesStamped.Images.Count)
				{
					imagesStamped.Images[i] = imageStamped.Image;

#if USE_AVERAGE_TIME_FOR_IMAGE_SYNC
					imageStampTimeSum += ;
					imageStampedCount++;
#else
					if (imageStamped.Time.Get() > imagesStampedTime)
					{
						imagesStampedTime = imageStamped.Time.Get();
					}
#endif
				}
			}
#if USE_AVERAGE_TIME_FOR_IMAGE_SYNC
			var imagesStampedAvgTime = imageStampTimeSum/(float)imageStampedCount;
			imagesStamped.Time.Set(imagesStampedAvgTime);
#else
			imagesStamped.Time.Set(imagesStampedTime);
#endif

			PushDeviceMessage<messages.ImagesStamped>(imagesStamped);
		}

		public void AddCamera(in SensorDevices.Camera newCam)
		{
			cameras.Add(newCam);
		}

		public SensorDevices.Camera GetCamera(in string cameraName)
		{
			var target = "::" + cameraName;
			return cameras.Find(x => x.DeviceName.EndsWith(target));
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