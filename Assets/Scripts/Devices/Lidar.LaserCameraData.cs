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
				imageBuffer = new byte[width * height * 4];
			}

			public void SetBufferData(in NativeArray<byte> buffer)
			{
				buffer.CopyTo(imageBuffer);
			}

			public byte[] GetBufferData()
			{
				return imageBuffer;
			}
		}


		struct LaserData
		{
			[ReadOnly]
			public byte[] data;

			[ReadOnly]
			public int width;

			[ReadOnly]
			public int height;

			private float DecodeChannel(in byte r, in byte g, in byte b, in byte a)
			{
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f;
				// decodedData = (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f);
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f + (a / 255f) / 16581375f;
				return (r * 0.0039215686f) + (g * 0.0000153787f) + (b * 0.0000000603f) + (a * 0.0000000002f);
			}

			private float GetDecodedData(in int pixelOffsetX, in int pixelOffsetY)
			{
				if (data != null && data.Length > 0)
				{
					// Decode
					const int colorFormatUnit = 4;
					var imageOffsetX = colorFormatUnit * pixelOffsetX;
					var imageOffsetY = colorFormatUnit * width * pixelOffsetY;
					var imageOffset = imageOffsetY + imageOffsetX;

					byte r = data[imageOffset + 0];
					byte g = data[imageOffset + 1];
					byte b = data[imageOffset + 2];
					byte a = data[imageOffset + 3];

					return DecodeChannel(r, g, b, a);
				}
				else
				{
					return 0;
				}
			}

			public float GetDepthData(in float horizontalAngle, in float verticalAngle = 0)
			{
				var horizontalAngleInCamData = horizontalAngle * Mathf.Deg2Rad;
				var verticalAngleInCamData = verticalAngle * Mathf.Deg2Rad;

				var maxAngleTangent = Mathf.Tan(laserCameraHFovHalf * Mathf.Deg2Rad);
				var offsetY = (height * 0.5f) * (1f + (Mathf.Tan(verticalAngleInCamData) / maxAngleTangent));
				var offsetX = (width * 0.5f) * (1f + (Mathf.Tan(horizontalAngleInCamData) / maxAngleTangent));

				var decodedData = GetDecodedData((int)offsetX, (int)offsetY);

				// Compensate distance
				var compensateScale = (1f / Mathf.Cos(horizontalAngleInCamData));
				var finalDepthData = decodedData * compensateScale;

				// Cutoff
				return (finalDepthData > 1.0f) ? 1.0f : finalDepthData;
			}
		}
	}
}