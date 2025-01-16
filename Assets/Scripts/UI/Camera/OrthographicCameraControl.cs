/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class OrthographicCameraControl : CameraControl
{
	protected override Vector3 HandleMouseWheelScroll()
	{
		var orthographicSizeByWheel = Input.mouseScrollDelta.y * _wheelMoveOrthoSize;

		if (Camera.main.orthographicSize > orthographicSizeByWheel)
		{
			Camera.main.orthographicSize -= orthographicSizeByWheel;
		}
		return Vector3.zero;
	}

	protected override Vector3 HandleKeyboardDirection()
	{
		var movementAmout = Vector3.zero;

		if (Input.GetKey(KeyCode.W))
		{
			movementAmout.y += 1;
		}
		else if (Input.GetKey(KeyCode.S))
		{
			movementAmout.y -= 1;
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
			Camera.main.orthographicSize += _wheelMoveOrthoSize;
		}
		else if (Input.GetKey(KeyCode.F))
		{
			if (Camera.main.orthographicSize > _wheelMoveOrthoSize)
			{
				Camera.main.orthographicSize -= _wheelMoveOrthoSize;
			}
		}

		return movementAmout;
	}
}