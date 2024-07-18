/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public static partial class DeviceHelper
{
	public static Matrix4x4 MakeCustomProjectionMatrix(in float hFov, in float vFov, in float near, in float far)
	{
		// construct custom aspect ratio projection matrix
		// math from https://www.scratchapixel.com/lessons/3d-basic-rendering/perspective-and-orthographic-projection-matrix/opengl-perspective-projection-matrix
		var h = 1.0f / Mathf.Tan(hFov * Mathf.Deg2Rad / 2f);
		var v = 1.0f / Mathf.Tan(vFov * Mathf.Deg2Rad / 2f);
		var a = (far + near) / (near - far);
		var b = (2.0f * far * near / (near - far));

		var projMatrix = new Matrix4x4(
			new Vector4(h, 0, 0, 0),
			new Vector4(0, v, 0, 0),
			new Vector4(0, 0, a, -1),
			new Vector4(0, 0, b, 0));

		return projMatrix;
	}

	public static float HorizontalToVerticalFOV(in float horizontalFOV, in float aspect = 1.0f)
	{
		return Mathf.Rad2Deg * 2 * Mathf.Atan(Mathf.Tan((horizontalFOV * Mathf.Deg2Rad) / 2f) / aspect);
	}
}