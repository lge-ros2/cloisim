/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.InputSystem;

public class OrthographicCameraControl : CameraControl
{
	protected override Vector3 HandleMouseWheelScroll()
	{
		var orthographicSizeByWheel = Mouse.current.scroll.ReadValue().y / 120f * _wheelMoveOrthoSize;

		if (Camera.main.orthographicSize > orthographicSizeByWheel)
		{
			Camera.main.orthographicSize -= orthographicSizeByWheel;
		}
		return Vector3.zero;
	}

	protected override Vector3 HandleKeyboardDirection()
	{
		var movementAmout = Vector3.zero;

		if (Keyboard.current[Key.W].isPressed)
		{
			movementAmout.y += 1;
		}
		else if (Keyboard.current[Key.S].isPressed)
		{
			movementAmout.y -= 1;
		}

		if (Keyboard.current[Key.A].isPressed && Keyboard.current[Key.D].isPressed)
		{
			movementAmout.x = 0;
		}
		else if (Keyboard.current[Key.A].isPressed)
		{
			movementAmout.x -= 1;
		}
		else if (Keyboard.current[Key.D].isPressed)
		{
			movementAmout.x += 1;
		}

		if (Keyboard.current[Key.G].isPressed)
		{
			Camera.main.orthographicSize += _wheelMoveOrthoSize;
		}
		else if (Keyboard.current[Key.F].isPressed)
		{
			if (Camera.main.orthographicSize > _wheelMoveOrthoSize)
			{
				Camera.main.orthographicSize -= _wheelMoveOrthoSize;
			}
		}

		return movementAmout;
	}
}