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
		struct LaserCamData
		{
			const int colorFormatUnit = sizeof(float);

			private int index;
			private float centerAngle;
			private int imageWidth;
			private int imageHeight;
			private byte[] imageBuffer;

			public float CenterAngle
			{
				get => centerAngle;
				set => centerAngle = value;
			}

			public int ImageWidth => imageWidth;

			public int ImageHeight => imageHeight;

			public void AllocateBuffer(in int dataIndex, in int width, in int height)
			{
				index = dataIndex;
				imageWidth = width;
				imageHeight = height;
				imageBuffer = new byte[width * height * colorFormatUnit];
			}

			public void SetBufferData(in NativeArray<byte> buffer)
			{
				if (imageBuffer != null)
				{
					buffer.CopyTo(imageBuffer);
				}
			}

			public float GetDepthData(in float horizontalAngle, in float verticalAngle = 0)
			{
				var horizontalAngleInCamData = (horizontalAngle - centerAngle) * Mathf.Deg2Rad;
				var verticalAngleInCamData = verticalAngle * Mathf.Deg2Rad;

				var maxAngleTangent = Mathf.Tan(laserCameraHFovHalf * Mathf.Deg2Rad);
				var offsetY = (imageHeight * 0.5f) * (1f + (Mathf.Tan(verticalAngleInCamData) / maxAngleTangent));
				var offsetX = (imageWidth * 0.5f) * (1f + (Mathf.Tan(horizontalAngleInCamData) / maxAngleTangent));

				var decodedData = GetDecodedData((int)offsetX, (int)offsetY);

				// Compensate distance
				var compensateScale = (1f / Mathf.Cos(horizontalAngleInCamData));
				var finalDepthData = decodedData * compensateScale;

				// Cutoff
				return (finalDepthData > 1.0f) ? 1.0f : finalDepthData;
			}

			private float DecodeChannel(in byte r, in byte g, in byte b, in byte a)
			{
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f;
				// decodedData = (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f);
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f + (a / 255f) / 16581375f;
				return (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f) + (a * 0.0000000002f);
			}

			private float GetDecodedData(in int pixelOffsetX, in int pixelOffsetY)
			{
				if (imageBuffer != null && imageBuffer.Length > 0)
				{
					// Decode
					var imageOffsetX = colorFormatUnit * pixelOffsetX;
					var imageOffsetY = colorFormatUnit * imageWidth * pixelOffsetY;
					var imageOffset = imageOffsetY + imageOffsetX;

					byte r = imageBuffer[imageOffset + 0];
					byte g = imageBuffer[imageOffset + 1];
					byte b = imageBuffer[imageOffset + 2];
					byte a = imageBuffer[imageOffset + 3];

					return DecodeChannel(r, g, b, a);
				}
				else
				{
					return 0;
				}
			}
		}
	}
}