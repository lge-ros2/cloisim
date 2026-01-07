/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using System;

namespace SensorDevices
{
	namespace LaserData
	{
		readonly public struct Scan
		{
			public readonly uint samples;
			public readonly double resolution;
			public readonly MathUtil.MinMax angle; // degree
			public readonly double angleStep;

			public Scan(in uint samples, in double angleMinRad, in double angleMaxRad, in double resolution)
			{
				this.samples = samples;
				this.resolution = resolution;
				this.angle = new MathUtil.MinMax(angleMinRad * Mathf.Rad2Deg, angleMaxRad * Mathf.Rad2Deg);

				if (Math.Abs(this.angle.range) < Quaternion.kEpsilon)
				{
					this.angleStep = 1;
				}
				else
				{
					var rangeCount = resolution * samples;
					this.angleStep = (rangeCount <= 0) ? 0 : (this.angle.range / rangeCount);

					var residual = (Math.Abs(360d - this.angle.range) < this.angleStep) ? 0 : 1;
					if (residual > 0 && rangeCount > 0)
					{
						this.angleStep = this.angle.range / (rangeCount + 1);
					}
				}
			}

			public Scan(in uint samples)
			{
				this.samples = samples;
				this.resolution = 1;
				this.angle = new MathUtil.MinMax();
				this.angleStep = 1;
			}
		}

		public struct AngleResolution
		{
			public readonly float H; // degree
			public readonly float V; // degree

			public AngleResolution(in float angleResolutionH = 0, in float angleResolutionV = 0)
			{
				this.H = angleResolutionH;
				this.V = angleResolutionV;
			}
		}

		public struct CameraControlInfo
		{
			public bool isOverlappingDirection;
			public float laserCamRotationalAngle;
		}

		public struct Output
		{
			public int dataIndex;
			public double[] rayData;

			public Output(in int index, in int bufferLength = 0)
			{
				dataIndex = index;
				rayData = bufferLength > 0 ? new double[bufferLength] : null;
			}

			public void ConvertDataType(Unity.Collections.NativeArray<float> src)
			{
				var offset = dataIndex * rayData.Length;
				for (var i = 0; i < rayData.Length; i++)
					rayData[i] = src[offset + i];
			}
		}
	}
}