/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.Rendering;

namespace SensorDevices
{
	namespace AsyncWork
	{
		public struct Camera
		{
			public AsyncGPUReadbackRequest? request;
			public float capturedTime;

			public Camera(in AsyncGPUReadbackRequest? request, in float capturedTime)
			{
				this.request = request;
				this.capturedTime = capturedTime;
			}
		}

		public struct Laser
		{
			public int dataIndex;
			public AsyncGPUReadbackRequest? request;
			public float capturedTime;
			public UnityEngine.Pose worldPose;

			public Laser(in int dataIndex, in AsyncGPUReadbackRequest? request, in float capturedTime, in UnityEngine.Pose worldPose)
			{
				this.dataIndex = dataIndex;
				this.request = request;
				this.capturedTime = capturedTime;
				this.worldPose = worldPose;
			}
		}
	}
}