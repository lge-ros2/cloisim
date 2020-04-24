/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using Unity.Collections;
using UnityEngine;

namespace SensorDevices
{
	public partial class Camera : Device
	{
		public enum PixelFormat
		{
			UNKNOWN_PIXEL_FORMAT = 0, L_INT8, L_INT16,
			RGB_INT8, RGBA_INT8, BGRA_INT8, RGB_INT16, RGB_INT32,
			BGR_INT8, BGR_INT16, BGR_INT32,
			R_FLOAT16, RGB_FLOAT16, R_FLOAT32, RGB_FLOAT32,
			BAYER_RGGB8, BAYER_RGGR8, BAYER_GBRG8, BAYER_GRBG8,
			PIXEL_FORMAT_COUNT
		};

		static public PixelFormat GetPixelFormat(in string imageFormat)
		{
			var parsedEnum = PixelFormat.UNKNOWN_PIXEL_FORMAT;

			if (imageFormat == null || imageFormat.Equals(string.Empty))
			{
				return parsedEnum;
			}

			// Handle old format strings
			if (imageFormat.Equals("L8") || imageFormat.Equals("L_INT8"))
			{
				parsedEnum = PixelFormat.L_INT8;
			}
			else if (imageFormat.Equals("L16") || imageFormat.Equals("L_INT16") || imageFormat.Equals("L_UINT16"))
			{
				// Note: we are treating unsigned and signed 16bit the same but it is
				// better to add a L_UINT16 format to distinguish between the two
				parsedEnum = PixelFormat.L_INT16;
			}
			else if (imageFormat.Equals("R8G8B8") || imageFormat.Equals("RGB_INT8"))
			{
				parsedEnum = PixelFormat.RGB_INT8;
			}
			else if (imageFormat.Equals("R16G16B16") || imageFormat.Equals("RGB_INT16")|| imageFormat.Equals("RGB_UINT16"))
			{
				// Note: we are treating unsigned and signed 16bit the same but it is
				// better to add a RGB_UINT16 format to distinguish between the two
				parsedEnum = PixelFormat.RGB_INT16;
			}
			else
			{
				parsedEnum = (PixelFormat)Enum.Parse(typeof(PixelFormat), imageFormat);
			}

			return parsedEnum;
		}

		static public uint GetImageDepth(in string imageFormat)
		{
			uint depth = 0;

			if (imageFormat == null || imageFormat.Equals(string.Empty))
			{
				return depth;
			}

			if (imageFormat.Equals("L8") || imageFormat.Equals("L_INT8"))
			{
				depth = 1;
			}
			else if (imageFormat.Equals("L16") || imageFormat.Equals("L_INT16")|| imageFormat.Equals("L_UINT16"))
			{
				depth = 2;
			}
			else if (imageFormat.Equals("R8G8B8")|| imageFormat.Equals("RGB_INT8"))
			{
				depth = 3;
			}
			else if (imageFormat.Equals("B8G8R8")|| imageFormat.Equals("BGR_INT8"))
			{
				depth = 3;
			}
			else if (imageFormat.Equals("R_FLOAT32"))
			{
				depth = 4;
			}
			else if (imageFormat.Equals("R16G16B16")|| imageFormat.Equals("RGB_INT16")|| imageFormat.Equals("RGB_UINT16"))
			{
				depth = 6;
			}
			else if (imageFormat.Equals("BAYER_RGGB8") || imageFormat.Equals("BAYER_BGGR8") ||
					 imageFormat.Equals("BAYER_GBRG8") || imageFormat.Equals("BAYER_GRBG8"))
			{
				depth = 1;
			}
			else
			{
				Debug.LogErrorFormat("Error parsing image format ({0}), using default PF_R8G8B8", imageFormat);
			}

			return depth;
		}

		private struct CamData
		{
			private Texture2D cameraImage;
			private Rect pixelSource;

			public int ImageWidth
			{
				get => cameraImage.width;
			}

			public int ImageHeight
			{
				get => cameraImage.height;
			}

			public void AllocateTexture(in int width, in int height, in string imageFormat)
			{
				var isLinear = false;

				var format = GetPixelFormat(imageFormat);
				var textureFormat = TextureFormat.RGB24;
				switch (format)
				{
					case PixelFormat.L_INT8:
						textureFormat = TextureFormat.R8;
						break;

					case PixelFormat.L_INT16:
						textureFormat = TextureFormat.RG16;
						break;

					case PixelFormat.R_FLOAT32:
						textureFormat = TextureFormat.RFloat;
						isLinear = true;
						break;

					case PixelFormat.RGB_FLOAT32:
						textureFormat = TextureFormat.RGBAFloat;
						isLinear = true;
						break;

					case PixelFormat.RGB_INT8:
					default:
						textureFormat = TextureFormat.RGB24;
						break;
				}

				cameraImage = new Texture2D(width, height, textureFormat, false, isLinear);
				pixelSource = new Rect(0, 0, width, height);
			}

			public void SetTextureData(in RenderTexture rt)
			{
				if (rt != null)
				{
					var currentRenderTexture = RenderTexture.active;
					RenderTexture.active = rt;
					cameraImage.ReadPixels(pixelSource, 0, 0);
					cameraImage.Apply();
					RenderTexture.active = currentRenderTexture;
				}
			}

			public void SetTextureData(in NativeArray<byte> buffer)
			{
				cameraImage.LoadRawTextureData<byte>(buffer);
				cameraImage.Apply();
			}

			public byte[] GetTextureData()
			{
				return cameraImage.GetRawTextureData();
			}

			public void SaveRawImageData(in string path, in string name)
			{
				var bytes = cameraImage.EncodeToPNG();
				var fileName = string.Format("{0}/{1}", path, name);
				System.IO.File.WriteAllBytes(fileName, bytes);
			}
		}

		private CamData camData;
	}
}