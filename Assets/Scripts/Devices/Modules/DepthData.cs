/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Unity.Collections;
using Unity.Jobs;

namespace SensorDevices
{
	public static class DepthData
	{
		private const int ColorFormatUnitSize = sizeof(float);

		public struct CamBuffer : IJobParallelFor
		{

			[ReadOnly]
			public NativeArray<byte> raw;

			public NativeArray<float> depth;
			private readonly int depthLength;

			public CamBuffer(in int width, in int height)
			{
				this.raw = default(NativeArray<byte>);
				this.depthLength = width * height;
				this.depth = default(NativeArray<float>);
			}

			public void Allocate()
			{
				this.depth = new NativeArray<float>(this.depthLength, Allocator.TempJob);
			}

			public void Deallocate()
			{
				this.raw.Dispose();
				this.depth.Dispose();
			}

			public int Length()
			{
				return depth.Length;
			}

			public void Execute(int i)
			{
				depth[i] = GetDecodedData(i);
			}

			private float GetDecodedData(in int index)
			{
				var imageOffset = index * ColorFormatUnitSize;
				if (raw != null && imageOffset < raw.Length)
				{
					var r = raw[imageOffset];
					var g = raw[imageOffset + 1];
					var b = raw[imageOffset + 2];
					var a = raw[imageOffset + 3];

					return DecodeFloatRGBA(r, g, b, a);
				}
				else
				{
					return 0;
				}
			}

			private float DecodeFloatRGBA(in byte r, in byte g, in byte b, in byte a)
			{
				// decodedData = (r / 255f) + (g / 255f) / 255f + (b / 255f) / 65025f + (a / 255f) / 16581375f;
				return (r * 0.00392156862f) + (g * 0.0000153787f) + (b * 0.0000000603086f) + (a * 0.0000000002365f);
			}
		}
	}
}