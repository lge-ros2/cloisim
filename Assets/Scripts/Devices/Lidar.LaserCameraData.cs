/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Unity.Collections;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		const int colorFormatUnitSize = sizeof(float);

		struct LaserCamData
		{
			public float maxHAngleHalf;
			public float maxHAngleHalfTangent;
			public float angleResolutionH;
			public float centerAngle;

			private int imageWidth;
			private int imageHeight;
			private byte[] imageBuffer;

			public double[] output;

			public float StartAngle => centerAngle - maxHAngleHalf;
			public float EndAngle => centerAngle + maxHAngleHalf;

			public void AllocateBuffer(in int width, in int height)
			{
				imageWidth = width;
				imageHeight = height;
				imageBuffer = new byte[width * height * colorFormatUnitSize];

				output = new double[imageWidth];
			}

			public void SetBufferData(in NativeArray<byte> buffer)
			{
				if (imageBuffer != null)
				{
					buffer.CopyTo(imageBuffer);
				}
			}

			public void ResolveLaserRanges(in float rangeMax = 1.0f)
			{
				for (var index = 0; index < output.Length; index++)
				{
					var rayAngleH = angleResolutionH * index;
					var depthData = GetDepthData(rayAngleH);
					var rayDistance = (depthData > 0) ? depthData * rangeMax : Mathf.Infinity;

					// CCW for ROS2 message direction
					var ccwIndex = output.Length - index - 1;
					output[ccwIndex] = rayDistance;
				}
			}

			private float GetDepthData(in float horizontalAngle, in float verticalAngle = 0)
			{
				var horizontalAngleInCamData = (horizontalAngle - maxHAngleHalf) * Mathf.Deg2Rad;
				var verticalAngleInCamData = verticalAngle * Mathf.Deg2Rad;

				var offsetX = (imageWidth * 0.5f) * (1f + Mathf.Tan(horizontalAngleInCamData)/maxHAngleHalfTangent);
				var offsetY = (imageHeight * 0.5f) * (1f + Mathf.Tan(verticalAngleInCamData)/maxHAngleHalfTangent);

				var decodedData = GetDecodedData((int)offsetX, (int)offsetY);

				// Compensate distance
				var compensateScale = (1f / Mathf.Cos(horizontalAngleInCamData));
				var finalDepthData = decodedData * compensateScale;

				// Cutoff
				return (finalDepthData > 1.0f) ? 1.0f : finalDepthData;
			}

			private float GetDecodedData(in int pixelOffsetX, in int pixelOffsetY)
			{
				if (imageBuffer != null && imageBuffer.Length > 0)
				{
					// Decode
					var imageOffsetX = colorFormatUnitSize * pixelOffsetX;
					var imageOffsetY = colorFormatUnitSize * imageWidth * pixelOffsetY;
					var imageOffset = imageOffsetY + imageOffsetX;

					var r = imageBuffer[imageOffset + 0];
					var g = imageBuffer[imageOffset + 1];
					var b = imageBuffer[imageOffset + 2];
					var a = imageBuffer[imageOffset + 3];

					return DecodeChannel(r, g, b, a);
				}
				else
				{
					return 0;
				}
			}

			private float DecodeChannel(in byte r, in byte g, in byte b, in byte a)
			{
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f;
				// decodedData = (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f);
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f + (a / 255f) / 16581375f;
				return (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f) + (a * 0.0000000002f);
			}
		}
	}
}