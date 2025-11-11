/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
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

		public struct LaserDataOutput
		{
			public double[] data;

			public float capturedTime;

			public LaserDataOutput(in int length = 0)
			{
				data = (length == 0) ? null : new double[length];
				capturedTime = 0;
			}
		}

		[BurstCompile]
		public struct LaserCamData : IJobParallelFor
		{
			private float maxHAngleHalf;
			private float maxHAngleHalfTanInverse;
			private float maxVAngleHalf;
			private float maxVAngleHalfTanInverse;
			public AngleResolution angleResolution;
			public float centerAngle;
			public MathUtil.MinMax range;

			public readonly int horizontalBufferLength;
			public readonly int horizontalBufferLengthHalf;
			public readonly int verticalBufferLength;
			public readonly int verticalBufferLengthHalf;

			[ReadOnly]
			public NativeArray<float> depthBuffer;

			private NativeArray<double> rayData;

			public float StartAngleH;
			public float EndAngleH;
			public float TotalAngleH;

			public LaserCamData(
					in int bufferWidth, in int bufferHeight,
			 		in MathUtil.MinMax range, in AngleResolution angleResolution,
					in float centerAngle,
					in float halfHFovAngle, in float halfVFovAngle)
			{
				this.maxHAngleHalf = halfHFovAngle;
				this.maxHAngleHalfTanInverse = 1 / Mathf.Tan(maxHAngleHalf * Mathf.Deg2Rad);
				this.maxVAngleHalf = halfVFovAngle;
				this.maxVAngleHalfTanInverse = 1 / Mathf.Tan(maxVAngleHalf * Mathf.Deg2Rad);
				this.angleResolution = angleResolution;

				this.centerAngle = centerAngle;
				this.StartAngleH = centerAngle - maxHAngleHalf;
				this.EndAngleH = centerAngle + maxHAngleHalf;
				this.TotalAngleH = this.maxHAngleHalf * 2f;

				this.range = range;
				this.horizontalBufferLength = bufferWidth;
				this.horizontalBufferLengthHalf = (int)(bufferWidth >> 1);
				this.verticalBufferLength = bufferHeight;
				this.verticalBufferLengthHalf = (int)(bufferHeight >> 1);
				this.depthBuffer = default(NativeArray<float>);
				this.rayData = default(NativeArray<double>);
			}

			public void Allocate()
			{
				var dataLength = horizontalBufferLength * verticalBufferLength;
				this.rayData = new NativeArray<double>(dataLength, Allocator.TempJob);
			}

			public void Deallocate()
			{
				this.rayData.Dispose();
			}

			public int OutputLength()
			{
				return rayData.Length;
			}

			private float GetDepthRange(in int offsetX, in int offsetY)
			{
				var safeOffsetX = Mathf.Clamp(offsetX, 0, horizontalBufferLength - 1);
    			var safeOffsetY = Mathf.Clamp(offsetY, 0, verticalBufferLength - 1);

    			var bufferOffset = (horizontalBufferLength * safeOffsetY) + safeOffsetX;
				// Debug.LogFormat("OffsetX: {0}, OffsetY: {1}", offsetX, offsetY);
				return depthBuffer[bufferOffset];
			}

			private float GetDepthData(in float horizontalAngle, in float verticalAngle)
			{
				var horizontalAngleInCam = (horizontalAngle - maxHAngleHalf) * Mathf.Deg2Rad;
				var verticalAngleInCam = (verticalAngle - maxVAngleHalf) * Mathf.Deg2Rad;

				var offsetYratio = Mathf.Tan(verticalAngleInCam) * maxVAngleHalfTanInverse;
				var offsetY = Mathf.CeilToInt(verticalBufferLengthHalf * (1f + offsetYratio));

				var offsetXratio = Mathf.Tan(horizontalAngleInCam) * maxHAngleHalfTanInverse;
				var offsetX = Mathf.CeilToInt(horizontalBufferLengthHalf * (1f + offsetXratio));

				var depthRange = GetDepthRange(offsetX, offsetY);

				var horizontalCos = Mathf.Cos(horizontalAngleInCam);
				var verticalCos = Mathf.Cos(verticalAngleInCam);
				depthRange *= 1 / (verticalCos * horizontalCos);

				return (depthRange > 1) ? Mathf.Infinity : depthRange;
			}

			private void ResolveLaserRange(in int index)
			{
				if (index >= OutputLength())
				{
					// Debug.LogWarning("index exceeded range " + index + " / " + OutputLength());
					return;
				}

				var indexH = (int)(index % horizontalBufferLength);
				var indexV = (int)(index / horizontalBufferLength);
				// Debug.Log("H: " + indexH + ", V:" + indexV);

				var rayAngleH = angleResolution.H * indexH;
				var rayAngleV = angleResolution.V * indexV;
				var depthData = GetDepthData(rayAngleH, rayAngleV);

				// filter min/max range
				var rayDistance = depthData * range.max;
				rayData[index] = (rayDistance < range.min) ? double.NaN : rayDistance;
			}

			// The code actually running on the job
			public void Execute(int i)
			{
				ResolveLaserRange(i);
			}

			public double[] GetLaserData()
			{
				return rayData.ToArray();
			}
		}
	}
}