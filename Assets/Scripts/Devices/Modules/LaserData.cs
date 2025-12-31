/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using UnityEngine.Rendering;
using System;

namespace SensorDevices
{
	public static class LaserData
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

				if (Math.Abs(this.angle.range) < double.Epsilon)
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

		public class Output : IDisposable
		{
			public bool isOverlapping;
			public float rotationAngle;
			public double capturedTime;
			public Pose worldPose;
			public double[] rayData;
			public ComputeBuffer computedRayBuffer;

			public Output(in float centerAngle, in int bufferLength, in bool overlapping)
			{
				isOverlapping = overlapping;
				rotationAngle = centerAngle;
				rayData = new double[bufferLength];
				capturedTime = 0;
				worldPose = Pose.identity;
				computedRayBuffer = new ComputeBuffer(bufferLength, sizeof(float));
			}

			public void ConvertData(AsyncGPUReadbackRequest req)
			{
				var srcSpan = req.GetData<float>().AsSpan();
				var dstSpan = rayData.AsSpan();

				var len = Math.Min(srcSpan.Length, dstSpan.Length);
				for (var i = 0; i < len; i++)
					dstSpan[i] = srcSpan[i];
			}

			public void Dispose()
			{
				if (computedRayBuffer != null)
				{
					computedRayBuffer.Release();
					computedRayBuffer = null;
				}
			}
		}
	}
}