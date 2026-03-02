/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System;
using System.Text;
using ProtoBuf;

public class DeviceMessage : MemoryStream
{
	// Magic numbers for raw binary image transport (little-endian)
	public const uint MAGIC_RAW_IMAGE = 0x52415749;       // "RAWI"
	public const uint MAGIC_RAW_SEGMENTATION = 0x52415753; // "RAWS"
	public const uint MAGIC_RAW_MULTI_IMAGE = 0x5241574D;  // "RAWM"

	// Fixed header size for single-image raw format (7 × uint32 = 28 bytes)
	public const int RAW_IMAGE_HEADER_SIZE = 28;

	// Reusable write buffer for header to avoid per-frame allocation
	private readonly byte[] _headerBuf = new byte[RAW_IMAGE_HEADER_SIZE];

	public DeviceMessage()
	{
		Reset();
	}

	public bool SetMessage(in byte[] data)
	{
		if (data == null)
		{
			return false;
		}

		if (CanWrite)
		{
			Reset();
			Write(data, 0, data.Length);
			Position = 0;
		}
		else
		{
			Console.WriteLine("Failed to write memory stream");
		}

		return true;
	}

	public void SetMessage<T>(T instance)
	{
		if (CanWrite)
		{
			Reset();
			try
			{
				Serializer.Serialize<T>(this, instance);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: SetMessage<{typeof(T).ToString()}>() during Serializer.Serialize: {ex.Message}");
			}
		}
		else
		{
			Console.WriteLine("Failed to write memory stream");
		}
	}

	/// <summary>
	/// Serialize an ImageStamped message as a compact binary blob:
	///   [magic:4][sec:4][nsec:4][width:4][height:4][pixel_format:4][step:4][pixel_data...]
	/// Bypasses protobuf entirely — zero-copy for the pixel payload.
	/// </summary>
	public void SetRawImage(cloisim.msgs.ImageStamped imgStamped)
	{
		if (!CanWrite) return;
		Reset();

		var img = imgStamped.Image;
		var data = img.Data;
		var dataLen = (data != null) ? data.Length : 0;

		// Ensure capacity to avoid multiple resizes
		var totalLen = RAW_IMAGE_HEADER_SIZE + dataLen;
		if (Capacity < totalLen)
			Capacity = totalLen;

		// Write 28-byte header into reusable buffer
		WriteUInt32LE(_headerBuf, 0, MAGIC_RAW_IMAGE);
		WriteInt32LE(_headerBuf, 4, imgStamped.Time.Sec);
		WriteInt32LE(_headerBuf, 8, imgStamped.Time.Nsec);
		WriteUInt32LE(_headerBuf, 12, img.Width);
		WriteUInt32LE(_headerBuf, 16, img.Height);
		WriteUInt32LE(_headerBuf, 20, img.PixelFormat);
		WriteUInt32LE(_headerBuf, 24, img.Step);

		Write(_headerBuf, 0, RAW_IMAGE_HEADER_SIZE);

		// Write raw pixel data — no protobuf framing, no extra copy
		if (dataLen > 0)
			Write(data, 0, dataLen);
	}

	/// <summary>
	/// Serialize a Segmentation message as:
	///   [RAWS header (28 bytes)][pixel_data...][class_count:4][per-class: class_id:4 + name_len:2 + name_bytes...]
	/// </summary>
	public void SetRawSegmentation(cloisim.msgs.Segmentation seg)
	{
		if (!CanWrite) return;
		Reset();

		var imgStamped = seg.ImageStamped;
		var img = imgStamped.Image;
		var data = img.Data;
		var dataLen = (data != null) ? data.Length : 0;

		// Write 28-byte header with segmentation magic
		WriteUInt32LE(_headerBuf, 0, MAGIC_RAW_SEGMENTATION);
		WriteInt32LE(_headerBuf, 4, imgStamped.Time.Sec);
		WriteInt32LE(_headerBuf, 8, imgStamped.Time.Nsec);
		WriteUInt32LE(_headerBuf, 12, img.Width);
		WriteUInt32LE(_headerBuf, 16, img.Height);
		WriteUInt32LE(_headerBuf, 20, img.PixelFormat);
		WriteUInt32LE(_headerBuf, 24, img.Step);

		Write(_headerBuf, 0, RAW_IMAGE_HEADER_SIZE);

		// Pixel data
		if (dataLen > 0)
			Write(data, 0, dataLen);

		// Class map suffix
		var classMapCount = seg.ClassMaps.Count;
		var countBuf = new byte[4];
		WriteUInt32LE(countBuf, 0, (uint)classMapCount);
		Write(countBuf, 0, 4);

		foreach (var vc in seg.ClassMaps)
		{
			var nameBuf = new byte[6]; // class_id(4) + name_len(2)
			WriteUInt32LE(nameBuf, 0, vc.ClassId);
			var nameBytes = Encoding.UTF8.GetBytes(vc.ClassName ?? "");
			WriteUInt16LE(nameBuf, 4, (ushort)nameBytes.Length);
			Write(nameBuf, 0, 6);
			if (nameBytes.Length > 0)
				Write(nameBytes, 0, nameBytes.Length);
		}
	}

	/// <summary>
	/// Serialize an ImagesStamped (multi-camera) message as:
	///   [RAWM:4][sec:4][nsec:4][image_count:4]
	///   per image: [width:4][height:4][pixel_format:4][step:4][pixel_data...]
	/// </summary>
	public void SetRawImagesStamped(cloisim.msgs.ImagesStamped imgsStamped)
	{
		if (!CanWrite) return;
		Reset();

		var imageCount = imgsStamped.Images.Count;

		// 16-byte shared header
		var sharedHeader = new byte[16];
		WriteUInt32LE(sharedHeader, 0, MAGIC_RAW_MULTI_IMAGE);
		WriteInt32LE(sharedHeader, 4, imgsStamped.Time.Sec);
		WriteInt32LE(sharedHeader, 8, imgsStamped.Time.Nsec);
		WriteUInt32LE(sharedHeader, 12, (uint)imageCount);
		Write(sharedHeader, 0, 16);

		// Per-image blocks: 16-byte sub-header + pixel data
		var subHeader = new byte[16];
		foreach (var img in imgsStamped.Images)
		{
			WriteUInt32LE(subHeader, 0, img.Width);
			WriteUInt32LE(subHeader, 4, img.Height);
			WriteUInt32LE(subHeader, 8, img.PixelFormat);
			WriteUInt32LE(subHeader, 12, img.Step);
			Write(subHeader, 0, 16);

			var data = img.Data;
			if (data != null && data.Length > 0)
				Write(data, 0, data.Length);
		}
	}

	public T GetMessage<T>()
	{
		Position = 0;

		T result;
		try
		{
			result = Serializer.Deserialize<T>(this);
		}
		catch (Exception)
		{
			result = default(T);
		}

		return result;
	}

	public void Reset()
	{
		Flush();
		SetLength(0);
		Position = 0;
		// Note: Do NOT reset Capacity — keep internal buffer warm to avoid GC reallocation
	}

	public bool IsValid()
	{
		return (CanRead && Length > 0);
	}

	// --- Little-endian binary helpers (avoid BinaryWriter allocation) ---

	private static void WriteUInt32LE(byte[] buf, int offset, uint value)
	{
		buf[offset] = (byte)(value);
		buf[offset + 1] = (byte)(value >> 8);
		buf[offset + 2] = (byte)(value >> 16);
		buf[offset + 3] = (byte)(value >> 24);
	}

	private static void WriteInt32LE(byte[] buf, int offset, int value)
	{
		buf[offset] = (byte)(value);
		buf[offset + 1] = (byte)(value >> 8);
		buf[offset + 2] = (byte)(value >> 16);
		buf[offset + 3] = (byte)(value >> 24);
	}

	private static void WriteUInt16LE(byte[] buf, int offset, ushort value)
	{
		buf[offset] = (byte)(value);
		buf[offset + 1] = (byte)(value >> 8);
	}
}