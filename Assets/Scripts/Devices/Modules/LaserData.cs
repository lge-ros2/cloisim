/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace SensorDevices
{
	public static class LaserData
	{
		readonly public struct MinMax
		{
			public readonly double min;
			public readonly double max;
			public readonly double range;

			public MinMax(in double min = 0, in double max = 0)
			{
				this.min = min;
				this.max = max;
				this.range = max - min;
			}
		}

		readonly public struct Scan
		{
			public readonly uint samples;
			public readonly double resolution;
			public readonly MinMax angle; // degree
			public readonly double angleStep;

			public Scan(in uint samples, in double angleMinRad, in double angleMaxRad, in double resolution = 1)
			{
				this.samples = samples;
				this.resolution = resolution;
				this.angle = new MinMax(angleMinRad * Mathf.Rad2Deg, angleMaxRad * Mathf.Rad2Deg);

				var residual = (angle.range - 360d < double.Epsilon ) ? 0 : 1;
				var rangeCount = resolution * samples - residual;

				this.angleStep = (rangeCount <= 0) ? 0 : ((angle.range) / rangeCount);
			}

			public Scan(in uint samples)
			{
				this.samples = samples;
				this.resolution = 1;
				this.angle = new MinMax();
				this.angleStep = 1;
			}
		}

		const int ColorFormatUnitSize = sizeof(float);

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

		public struct DepthCamBuffer : IJobParallelFor
		{
			private readonly int imageWidth;
			private readonly int imageHeight;

			[ReadOnly]
			public NativeArray<byte> imageBuffer;
			public NativeArray<float> depthBuffer;

			public DepthCamBuffer(in int width, in int height)
			{
				this.imageWidth = width;
				this.imageHeight = height;
				this.imageBuffer = default(NativeArray<byte>);
				this.depthBuffer = default(NativeArray<float>);
			}

			public void Allocate()
			{
				var dataLength = imageWidth * imageHeight;
				this.depthBuffer = new NativeArray<float>(dataLength, Allocator.TempJob);
			}

			public void Deallocate()
			{
				this.depthBuffer.Dispose();
			}

			public int Length()
			{
				return depthBuffer.Length;
			}

			public void Execute(int i)
			{
				depthBuffer[i] = GetDecodedData(i);
			}

			private float GetDecodedData(in int index)
			{
				var imageOffset = index * ColorFormatUnitSize;
				if (imageBuffer != null && imageOffset < imageBuffer.Length)
				{
					var r = imageBuffer[imageOffset];
					var g = imageBuffer[imageOffset + 1];
					var b = imageBuffer[imageOffset + 2];
					var a = imageBuffer[imageOffset + 3];

					return DecodeFloatRGBA(r, g, b, a);
				}
				else
				{
					return 0;
				}
			}

			private float DecodeFloatRGBA(in byte r, in byte g, in byte b, in byte a)
			{
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f;
				// decodedData = (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f);
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f + (a / 255f) / 16581375f;
				return (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f) + (a * 0.0000000002f);
			}
		}

		public struct LaserDataOutput
		{
			public double[] data;

			public LaserDataOutput(in int length = 0)
			{
				data = (length == 0) ? null : new double[length];
			}
		}

		public struct LaserCamData : IJobParallelFor
		{
			private float maxHAngleHalf;
			private float maxHAngleHalfTangent;
			public AngleResolution angleResolution;
			public float centerAngle;
			public MinMax range;

			public readonly int horizontalBufferLength;
			public readonly int verticalBufferLength;

			[ReadOnly]
			public NativeArray<float> depthBuffer;

			private NativeArray<double> laserData;

			public float StartAngleH;
			public float EndAngleH;
			public float TotalAngleH;

			public LaserCamData(in int bufferWidth, in int bufferHeight, in AngleResolution angleResolution, in float centerAngle, in float halfHFovAngle)
			{
				this.maxHAngleHalf = halfHFovAngle;
				this.maxHAngleHalfTangent = Mathf.Tan(maxHAngleHalf * Mathf.Deg2Rad);
				this.angleResolution = angleResolution;

				this.centerAngle = centerAngle;
				this.StartAngleH = centerAngle - maxHAngleHalf;
				this.EndAngleH = centerAngle + maxHAngleHalf;
 				this.TotalAngleH = this.maxHAngleHalf * 2f;

				this.range = new MinMax();
				this.horizontalBufferLength = bufferWidth;
				this.verticalBufferLength = bufferHeight;
				this.depthBuffer = default(NativeArray<float>);
				this.laserData = default(NativeArray<double>);
			}

			public void Allocate()
			{
				var dataLength = horizontalBufferLength * verticalBufferLength;
				this.laserData = new NativeArray<double>(dataLength, Allocator.TempJob);
			}

			public void Deallocate()
			{
				this.laserData.Dispose();
			}

			public int OutputLength()
			{
				return laserData.Length;
			}

			private float GetDepthRange(in int offsetX, in int offsetY)
			{
				var bufferOffset = (horizontalBufferLength * offsetY) + offsetX;
				// Debug.LogFormat("OffsetX: {0}, OffsetY: {1}", offsetX, offsetY);
				return depthBuffer[bufferOffset];
			}

			private float GetDepthData(in float horizontalAngle, in float verticalAngle)
			{
				var horizontalAngleInCamData = (horizontalAngle - maxHAngleHalf) * Mathf.Deg2Rad;
				var verticalAngleInCamData = verticalAngle * Mathf.Deg2Rad;

				var offsetX = Mathf.FloorToInt((horizontalBufferLength * 0.5f) * (1f + Mathf.Tan(horizontalAngleInCamData)/maxHAngleHalfTangent));
				var offsetY = Mathf.FloorToInt((verticalBufferLength * 0.5f) * (1f + Mathf.Tan(verticalAngleInCamData)/maxHAngleHalfTangent));

				var depthRange = GetDepthRange(offsetX, offsetY);

				// Compensate distance
				var compensateScale = (1f / Mathf.Cos(horizontalAngleInCamData));
				var finalDepthData = depthRange * compensateScale;
				return finalDepthData;
			}

			private void ResolveLaserRange(in int index)
			{
				if (index >= OutputLength())
				{
					Debug.LogWarning("index exceeded range " + index + " / " + OutputLength());
					return;
				}

				var indexH = index % horizontalBufferLength;
				var indexV = index / horizontalBufferLength;
				// Debug.Log("H: " + indexH + ", V:" + indexV);

				var rayAngleH = angleResolution.H * indexH;
				var rayAngleV = angleResolution.V * indexV;
				var depthData = GetDepthData(rayAngleH, rayAngleV);

				// filter range
				var rayDistance = (depthData > 1f) ? Mathf.Infinity : (depthData * range.max);
				if (rayDistance < range.min)
				{
					rayDistance = double.NaN;
				}

				laserData[index] = (double)rayDistance;
			}

			// The code actually running on the job
			public void Execute(int i)
			{
				ResolveLaserRange(i);
			}

			public double[] GetLaserData()
			{
				return laserData.ToArray();
			}
		}
	}
}