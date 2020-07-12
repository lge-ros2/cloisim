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
			private Texture2D cameraImage;

			public float CenterAngle
			{
				get => centerAngle;
				set => centerAngle = value;
			}

			public int ImageWidth
			{
				get => cameraImage.width;
			}

			public int ImageHeight
			{
				get => cameraImage.height;
			}

			public void AllocateTexture(in int dataIndex, in int width, in int height)
			{
				index = dataIndex;
				cameraImage = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			}

			public void SetTextureData(in NativeArray<byte> buffer)
			{
				cameraImage.LoadRawTextureData<byte>(buffer);
				cameraImage.Apply();
			}

			public NativeArray<byte> GetTextureData()
			{
				return cameraImage.GetRawTextureData<byte>();
			}

			public void SaveRawImageData(in string name)
			{
				var bytes = cameraImage.EncodeToPNG();
				var fileName = string.Format("./Logs/{0}_{1:00}_{2:000}", name, index, centerAngle);
				System.IO.File.WriteAllBytes(fileName, bytes);
			}
		}


		struct LaserData
		{
			[ReadOnly]
			public NativeArray<byte> data;

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
				if (data.IsCreated)
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