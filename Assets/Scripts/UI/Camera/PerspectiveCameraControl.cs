/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.InputSystem;

public class PerspectiveCameraControl : CameraControl
{
	protected override Vector3 HandleMouseWheelScroll()
	{
		return new Vector3(0, 0, Mouse.current.scroll.ReadValue().y / 120f * _wheelMoveAmp);
	}

	protected override Vector3 HandleKeyboardDirection()
	{
		var movementAmout = Vector3.zero;

		if (Keyboard.current[Key.W].isPressed)
		{
			movementAmout.z += 1;
		}
		else if (Keyboard.current[Key.S].isPressed)
		{
			movementAmout.z -= 1;
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
			movementAmout.y += 1;
		}
		else if (Keyboard.current[Key.F].isPressed)
		{
			movementAmout.y -= 1;
		}

		return movementAmout;
	}
}