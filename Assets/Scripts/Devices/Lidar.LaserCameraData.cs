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
			private int imageWidth;
			private int imageHeight;

			[ReadOnly]
			public NativeArray<byte> imageBuffer;

			public NativeArray<float> depthBuffer;

			public void AllocateDepthBuffer(in int width, in int height)
			{
				imageWidth = width;
				imageHeight = height;

				var dataLength = imageWidth * imageHeight;
				depthBuffer = new NativeArray<float>(dataLength, Allocator.Persistent);
			}

			public void DeallocateDepthBuffer()
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

		struct LaserCamData : IJobParallelFor
		{
			private float maxHAngleHalf;
			private float maxHAngleHalfTangent;
			public float angleResolutionH;
			public float centerAngle;
			public float rangeMax;

			private int horizontalBufferLength;
			private int verticalBufferLength;

			[ReadOnly]
			public NativeArray<float> depthBuffer;

			private NativeArray<double> laserDataOutput;

			public float StartAngle => centerAngle - maxHAngleHalf;
			public float EndAngle => centerAngle + maxHAngleHalf;
			public float TotalAngle => EndAngle - StartAngle;

			public void AllocateBuffer(in int bufferWidth, in int bufferHeight)
			{
				horizontalBufferLength = bufferWidth;
				verticalBufferLength = bufferHeight;

				// TODO: vertical ray
				var dataLength = horizontalBufferLength; //* verticalBufferLength;
				laserDataOutput = new NativeArray<double>(dataLength, Allocator.Persistent);
			}

			public void DeallocateBuffer()
			{
				laserDataOutput.Dispose();
			}

			public int OutputLength()
			{
				return laserDataOutput.Length;
			}

			public void SetMaxHorizontalHalfAngle(in float angle)
			{
				maxHAngleHalf = angle;
				maxHAngleHalfTangent = Mathf.Tan(maxHAngleHalf * Mathf.Deg2Rad);
			}

			private float GetDepthRange(in int offsetX, in int offsetY)
			{
				var bufferOffset = (horizontalBufferLength * offsetY) + offsetX;
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

				var rayAngleH = angleResolutionH * index;
				var depthData = GetDepthData(rayAngleH);
				var rayDistance = (depthData > 0) ? depthData * rangeMax : Mathf.Infinity;

				laserDataOutput[index] = (double)rayDistance;
			}

			// The code actually running on the job
			public void Execute(int i)
			{
				ResolveLaserRange(i);
			}

			public double[] GetOutputs()
			{
				var outputArray = laserDataOutput.ToArray();
				// CCW for ROS2 message direction
				Array.Reverse(outputArray);
				return outputArray;
			}
		}
	}
}