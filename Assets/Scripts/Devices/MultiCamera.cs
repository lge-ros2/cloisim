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
		private List<SensorDevices.Camera> cameras = new();

		// Pre-allocated reusable message objects to avoid per-frame GC allocations
		private messages.ImagesStamped _imagesStamped = null;

		// Buffered images from child cameras to avoid data loss
		// when cameras produce data at slightly different times
		private messages.Image[] _pendingImages = null;
		private float[] _pendingTimestamps = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
		}

		protected override void OnStart()
		{
			var count = cameras.Count;
			_pendingImages = new messages.Image[count];
			_pendingTimestamps = new float[count];
		}

		protected override void InitializeMessages()
		{
			_imagesStamped = new messages.ImagesStamped();
			_imagesStamped.Time = new messages.Time();
		}

		/// <summary>
		/// Called by the Device TX thread at each update interval.
		/// Collects images from all child cameras and pushes a combined
		/// ImagesStamped message. Buffers partially-collected images
		/// to avoid data loss when cameras produce data at different times.
		/// </summary>
		protected override void GenerateMessage()
		{
			if (cameras.Count == 0)
				return;

			// Phase 1: Try to collect from cameras that haven't produced data yet
			for (var i = 0; i < cameras.Count; i++)
			{
				if (_pendingImages[i] != null)
					continue;

				var msg = cameras[i].GetImageDataMessage();
				if (msg is messages.ImageStamped imageStampedMsg)
				{
					_pendingImages[i] = imageStampedMsg.Image;
					_pendingTimestamps[i] = imageStampedMsg.Time.Get();
				}
				else if (msg is messages.Segmentation segMsg)
				{
					_pendingImages[i] = segMsg.ImageStamped.Image;
					_pendingTimestamps[i] = segMsg.ImageStamped.Time.Get();
				}
			}

			// Phase 2: Check if all cameras have data
			for (var i = 0; i < cameras.Count; i++)
			{
				if (_pendingImages[i] == null)
					return;
			}

			// Phase 3: All ready — build and push
			_imagesStamped.Images.Clear();
			var latestTimestamp = 0f;

			for (var i = 0; i < cameras.Count; i++)
			{
				_imagesStamped.Images.Add(_pendingImages[i]);

				if (_pendingTimestamps[i] > latestTimestamp)
					latestTimestamp = _pendingTimestamps[i];

				_pendingImages[i] = null;
			}

			_imagesStamped.Time.Set(latestTimestamp);
			PushDeviceMessage(_imagesStamped);
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