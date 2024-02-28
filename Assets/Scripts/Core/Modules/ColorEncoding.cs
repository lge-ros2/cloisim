﻿/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */


using UnityEngine;

public class ColorEncoding
{
	// public static byte ReverseBits(byte value)
	// {
	//     return (byte)((value * 0x0202020202 & 0x010884422010) % 1023);
	// }

	public static int SparsifyBits(byte value, in int sparse)
	{
		int retVal = 0;
		for (int bits = 0; bits < 8; bits++, value >>= 1)
		{
			retVal |= (value & 1);
			retVal <<= sparse;
		}
		return retVal >> sparse;
	}

	public static Color EncodeIDAsColor(in int instanceId)
	{
		var uid = instanceId * 2;
		if (uid < 0)
			uid = -uid + 1;

		var sid =
			(SparsifyBits((byte)(uid >> 16), 3) << 2) |
			(SparsifyBits((byte)(uid >> 8), 3) << 1) |
			 SparsifyBits((byte)(uid), 3);
		//Debug.Log(uid + " >>> " + System.Convert.ToString(sid, 2).PadLeft(24, '0'));

		var r = (byte)(sid >> 8);
		var g = (byte)(sid >> 16);
		var b = (byte)(sid);
		// Debug.Log($"EncodeIDAsColor({instanceId}): {r} {g} {b}");
		return new Color32(r, g, b, 255);
	}

	public static Color EncodeNameAsColor(in string name)
	{
		return EncodeTagAsColor(name);
	}

	public static Color EncodeTagAsColor(in string tag)
	{
		var hash = tag.GetHashCode();
		var a = (byte)(hash >> 24);
		var r = (byte)(hash >> 16);
		var g = (byte)(hash >> 8);
		var b = (byte)(hash);
		// Debug.Log($"EncodeTagAsColor({tag}): {r} {g} {b}");
		return new Color32(r, g, b, 255);
	}

	public static Color EncodeLayerAsColor(in int layer)
	{
		// Following value must be in the range (0.5 .. 1.0)
		// in order to avoid color overlaps when using 'divider' in this func
		var z = .7f;

		var uniqueColors = new Color[] {
			new Color(1,1,1,1), new Color(z,z,z,1),                     // 0
			new Color(1,1,z,1), new Color(1,z,1,1), new Color(z,1,1,1), //
			new Color(1,z,0,1), new Color(z,0,1,1), new Color(0,1,z,1), // 7

			new Color(1,0,0,1), new Color(0,1,0,1), new Color(0,0,1,1), // 8
			new Color(1,1,0,1), new Color(1,0,1,1), new Color(0,1,1,1), //
			new Color(1,z,z,1), new Color(z,1,z,1)                      // 15
		};

		// Create as many colors as necessary by using base 16 color palette
		// To create more than 16 - will simply adjust brightness with 'divider'
		var color = uniqueColors[layer % uniqueColors.Length];
		var divider = 1.0f + Mathf.Floor(layer / uniqueColors.Length);
		color /= divider;

		return color;
	}
}
