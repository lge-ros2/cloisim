/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using Unity.Collections;
using UnityEngine.Rendering;
using UnityEngine;

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

			public Laser(in int dataIndex, in AsyncGPUReadbackRequest? request, in float capturedTime)
			{
				this.dataIndex = dataIndex;
				this.request = request;
				this.capturedTime = capturedTime;
			}
		}
	}
}