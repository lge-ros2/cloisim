/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using messages = cloisim.msgs;


namespace SensorDevices
{
	public class MultiCamera : Device
	{
		private List<SensorDevices.Camera> cameras = new List<SensorDevices.Camera>();
		private Thread _imagesProcessThread = null;
		private bool _runningThread = false;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
		}

		protected override void OnStart()
		{
			_imagesProcessThread = new Thread(MultiImageProcess);
			_imagesProcessThread.Start();
			_runningThread = true;
		}

		protected new void OnDestroy()
		{
			if (_imagesProcessThread != null && _imagesProcessThread.IsAlive)
			{
				_runningThread = false;
				_imagesProcessThread.Join();
				_imagesProcessThread.Abort();
			}

			base.OnDestroy();
		}

		private void MultiImageProcess()
		{
			while (_runningThread)
			{
				var imagesStamped = new messages.ImagesStamped();
				imagesStamped.Time = new messages.Time();

				var latestImageTimestamp = 0f;
				for (var i = 0; i < cameras.Count; i++)
				{
					// Set images data only once
					var image = new messages.Image();
					var imageStamped = cameras[i].GetImageDataMessage();
					if (imageStamped == null)
					{
						Debug.LogWarning($"MultiCam{i} is not ready");
						latestImageTimestamp = 0;
						break;
					}
					var timestamp = imagesStamped.Time.Get();
					if (timestamp > latestImageTimestamp)
					{
						latestImageTimestamp = timestamp;
					}
					imagesStamped.Images.Add(image);
				}

				if (latestImageTimestamp == 0)
				{
					continue;
				}
				else
				{
					imagesStamped.Time.Set(latestImageTimestamp);
					_messageQueue.Enqueue(imagesStamped);
				}

				Thread.Sleep(1);
			}
		}

		protected override void GenerateMessage()
		{
			while (_messageQueue.TryDequeue(out var msg))
			{
				PushDeviceMessage(msg);
			}
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