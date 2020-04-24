/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class MouseCursor : MonoBehaviour
{
	public Texture2D cursorTexture;
	public CursorMode cursorMode = CursorMode.Auto;
	public Vector2 hotSpot = Vector2.zero;
	void OnMouseEnter()
	{
		Cursor.SetCursor(cursorTexture, hotSpot, cursorMode);
	}

	void OnMouseExit()
	{
		Cursor.SetCursor(null, Vector2.zero, cursorMode);
	}
}
