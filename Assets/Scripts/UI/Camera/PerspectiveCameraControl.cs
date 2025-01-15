/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

[DefaultExecutionOrder(90)]
public class PerspectiveCameraControl : CameraControl
{
	protected override Vector3 HandleMouseWheelScroll()
	{
		return new Vector3(0, 0, Input.mouseScrollDelta.y * _wheelMoveAmp);
	}

	protected override Vector3 HandleKeyboardDirection()
	{
		var movementAmout = Vector3.zero;

		if (Input.GetKey(KeyCode.W))
		{
			movementAmout.z += 1;
		}
		else if (Input.GetKey(KeyCode.S))
		{
			movementAmout.z -= 1;
		}

		if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
		{
			movementAmout.x = 0;
		}
		else if (Input.GetKey(KeyCode.A))
		{
			movementAmout.x -= 1;
		}
		else if (Input.GetKey(KeyCode.D))
		{
			movementAmout.x += 1;
		}

		if (Input.GetKey(KeyCode.G))
		{
			movementAmout.y += 1;
		}
		else if (Input.GetKey(KeyCode.F))
		{
			movementAmout.y -= 1;
		}

		return movementAmout;
	}
}