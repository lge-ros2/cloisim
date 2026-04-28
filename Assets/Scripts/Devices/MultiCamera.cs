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
		private List<Camera> cameras = new();

		// Pre-allocated reusable message objects to avoid per-frame GC allocations
		private messages.Images _images = null;

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
			_images = new messages.Images
			{
				Time = new messages.Time()
			};
		}

		/// <summary>
		/// Called by the Device TX thread at each update interval.
		/// Collects images from all child cameras and pushes a combined
		/// Images message. Buffers partially-collected images
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
				if (msg is messages.Image imageMsg)
				{
					_pendingImages[i] = imageMsg;
					_pendingTimestamps[i] = imageMsg.Header.Stamp.Get();
				}
				else if (msg is messages.Segmentation segMsg)
				{
					_pendingImages[i] = segMsg.Image;
					_pendingTimestamps[i] = segMsg.Image.Header.Stamp.Get();
				}
			}

			// Phase 2: Check if all cameras have data
			for (var i = 0; i < cameras.Count; i++)
			{
				if (_pendingImages[i] == null)
					return;
			}

			// Phase 3: All ready — build and push
			_images.image.Clear();
			var latestTimestamp = 0f;

			for (var i = 0; i < cameras.Count; i++)
			{
				_images.image.Add(_pendingImages[i]);

				if (_pendingTimestamps[i] > latestTimestamp)
					latestTimestamp = _pendingTimestamps[i];

				_pendingImages[i] = null;
			}

			_images.Time.Set(latestTimestamp);
			PushDeviceMessage(_images);
		}

		public void AddCamera(in Camera newCam)
		{
			cameras.Add(newCam);
		}

		public Camera GetCamera(in string cameraName)
		{
			var target = "::" + cameraName;
			return cameras.Find(x => x.DeviceName.EndsWith(target));
		}

		public Camera GetCamera(in int cameraIndex)
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