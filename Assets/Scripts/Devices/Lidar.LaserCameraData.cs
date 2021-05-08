/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		const int colorFormatUnitSize = sizeof(float);

		struct DepthCamBuffer : IJobParallelFor
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

				var dataLength = imageWidth * imageHeight;
				depthBuffer = new NativeArray<float>(dataLength, Allocator.Persistent);
			}

			public void Deallocate()
			{
				depthBuffer.Dispose();
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
				var imageOffset = index * colorFormatUnitSize;
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

		struct LaserDataOutput
		{
			public double[] data;

			public LaserDataOutput(in int length = 0)
			{
        data = (length == 0) ? null : new double[length];
			}
		}

		struct LaserCamData : IJobParallelFor
		{
			private float maxHAngleHalf;
			private float maxHAngleHalfTangent;
			public AngleResolution angleResolution;
			public float centerAngle;
			public float rangeMax;

			public readonly int horizontalBufferLength;
			public readonly int verticalBufferLength;

			[ReadOnly]
			public NativeArray<float> depthBuffer;

			private NativeArray<double> laserData;


			public readonly float StartAngleH => centerAngle - maxHAngleHalf;
			public readonly float EndAngleH => centerAngle + maxHAngleHalf;
			public readonly float TotalAngleH => EndAngleH - StartAngleH;

			public LaserCamData(in int bufferWidth, in int bufferHeight, in AngleResolution angleResolution)
			{
				this.maxHAngleHalf = 0;
				this.maxHAngleHalfTangent = 0;
				this.angleResolution = angleResolution;
				this.centerAngle = 0;
				this.rangeMax = 0;
				this.horizontalBufferLength = bufferWidth;
				this.verticalBufferLength = bufferHeight;
				this.depthBuffer = default(NativeArray<float>);

				var dataLength = horizontalBufferLength * verticalBufferLength;
				this.laserData = new NativeArray<double>(dataLength, Allocator.Persistent);
			}

			public void Deallocate()
			{
				laserData.Dispose();
			}

			public int OutputLength()
			{
				return laserData.Length;
			}

			public void SetMaxHorizontalHalfAngle(in float angle)
			{
				maxHAngleHalf = angle;
				maxHAngleHalfTangent = Mathf.Tan(maxHAngleHalf * Mathf.Deg2Rad);
			}

			private float GetDepthRange(in int offsetX, in int offsetY)
			{
				var bufferOffset = (horizontalBufferLength * offsetY) + offsetX;
				// Debug.LogFormat("OffsetX: {0}, OffsetY: {1}", offsetX, offsetY);
				return depthBuffer[bufferOffset];
			}

			private float GetDepthData(in float horizontalAngle, in float verticalAngle = 0)
			{
				var horizontalAngleInCamData = (horizontalAngle - maxHAngleHalf) * Mathf.Deg2Rad;
				var verticalAngleInCamData = verticalAngle * Mathf.Deg2Rad;

				var offsetX = Mathf.FloorToInt((horizontalBufferLength * 0.5f) * (1f + Mathf.Tan(horizontalAngleInCamData)/maxHAngleHalfTangent));
				var offsetY = Mathf.FloorToInt((verticalBufferLength * 0.5f) * (1f + Mathf.Tan(verticalAngleInCamData)/maxHAngleHalfTangent));

				var depthRange = GetDepthRange(offsetX, offsetY);

				// Compensate distance
				var compensateScale = (1f / Mathf.Cos(horizontalAngleInCamData));
				var finalDepthData = depthRange * compensateScale;

				// Cutoff
				return (finalDepthData > 1.0f) ? 1.0f : finalDepthData;
			}

			private void ResolveLaserRange(in int index)
			{
				if (index >= OutputLength())
				{
					Debug.Log("index exceeded range " + index + " / " + OutputLength());
					return;
				}

				var indexH = index % horizontalBufferLength;
				var indexV = index / horizontalBufferLength;
				// Debug.Log("H: " + indexH + ", V:" + indexV);

				var rayAngleH = angleResolution.H * indexH;
				var rayAngleV = angleResolution.V * indexV;
				var depthData = GetDepthData(rayAngleH);
				var rayDistance = (depthData > 0) ? depthData * rangeMax : Mathf.Infinity;

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
