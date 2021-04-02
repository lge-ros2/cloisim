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
			switch (imageFormat)
			{
				case "L8":
				case "L_INT8":
					parsedEnum = PixelFormat.L_INT8;
					break;

				case "L16":
				case "L_INT16":
				case "L_UINT16":
					// Note: we are treating unsigned and signed 16bit the same but it is
					// better to add a L_UINT16 format to distinguish between the two
					parsedEnum = PixelFormat.L_INT16;
					break;

				case "R8G8B8":
				case "RGB_INT8":
					parsedEnum = PixelFormat.RGB_INT8;
					break;

				case "R16G16B16":
				case "RGB_INT16":
				case "RGB_UINT16":
					// Note: we are treating unsigned and signed 16bit the same but it is
					// better to add a RGB_UINT16 format to distinguish between the two
					parsedEnum = PixelFormat.RGB_INT16;
					break;

				default:
					parsedEnum = (PixelFormat)Enum.Parse(typeof(PixelFormat), imageFormat);
					break;
			}

			return parsedEnum;
		}

		static public int GetImageDepth(in PixelFormat pixelFormat)
		{
			var depth = 0;

			if (pixelFormat.Equals(PixelFormat.UNKNOWN_PIXEL_FORMAT))
			{
				return depth;
			}

			switch (pixelFormat)
			{
				case PixelFormat.L_INT8:
				case PixelFormat.BAYER_GBRG8:
				case PixelFormat.BAYER_GRBG8:
				case PixelFormat.BAYER_RGGB8:
				case PixelFormat.BAYER_RGGR8:
					depth = 1;
					break;

				case PixelFormat.L_INT16:
				case PixelFormat.R_FLOAT16:
					depth = 2;
					break;

				case PixelFormat.RGB_INT8:
				case PixelFormat.BGR_INT8:
					depth = 3;
					break;

				case PixelFormat.RGB_INT16:
				case PixelFormat.BGR_INT16:
					depth = 6;
					break;

				case PixelFormat.R_FLOAT32:
					depth = 4;
					break;

				default:
					Debug.LogErrorFormat("Error parsing image format ({0})", pixelFormat);
					break;
			}

			return depth;
		}

		private struct CamImageData
		{
			private NativeArray<byte> imageBuffer;

			private Texture2D cameraImage;

			public void AllocateTexture(in int width, in int height, in string imageFormat)
			{
				var isLinear = false;
				var textureFormat = TextureFormat.RGB24;

				var format = GetPixelFormat(imageFormat);
				switch (format)
				{
					case PixelFormat.L_INT8:
						textureFormat = TextureFormat.R8;
						break;

					case PixelFormat.L_INT16:
						textureFormat = TextureFormat.RG16;
						break;

					case PixelFormat.R_FLOAT32:
						textureFormat = TextureFormat.ARGB32;
						isLinear = true;
						break;

					case PixelFormat.RGB_FLOAT32:
						textureFormat = TextureFormat.ARGB32;
						isLinear = true;
						break;

					case PixelFormat.RGB_INT8:
					default:
						break;
				}

				cameraImage = new Texture2D(width, height, textureFormat, false, isLinear);
			}

			public void SetTextureBufferData(NativeArray<byte> buffer)
			{
				imageBuffer = buffer;
			}

			public int GetImageDataLength()
			{
				return imageBuffer.Length;
			}

			public byte[] GetImageData()
			{
				return imageBuffer.ToArray();
			}

			public void SaveRawImageData(in string path, in string name)
			{
				cameraImage.SetPixelData(imageBuffer, 0);
				cameraImage.Apply();
				var bytes = cameraImage.EncodeToPNG();
				var fileName = string.Format("{0}/{1}.png", path, name);
				System.IO.File.WriteAllBytes(fileName, bytes);
			}
		}

		protected virtual void BufferDepthScaling(ref byte[] buffer)
		{
		}
	}
}