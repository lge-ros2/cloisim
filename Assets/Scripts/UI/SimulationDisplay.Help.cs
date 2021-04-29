	/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;

public partial class SimulationDisplay : MonoBehaviour
{
	[Header("Help dialog")]
	private String _helpContents;
	private const int buttonWidthHelp = 65;
	private const int helpStatusWidth = buttonWidthHelp * 2;

	private bool _popupHelpDialog = false;

	private void UpdateHelpContents()
	{
		var sb = new StringBuilder();
		sb.AppendLine("How to manipulate");

// 		General
// Ctrl + R : Reset simulation Ctrl + Shift + R : simulation Full reset like restart

// Object spawning
// Choose one of model on the top of screen.[Box], [Cylinder], [Sphere]

// Pressing 'Left-Ctrl' Key,

// Spawning:
// (+) Click the left mouse button on the pointer
// Remove:
// (+) Click the right mouse button on the object already spawned
// Object Control
// After select object you want,

// T : Translation
// (+) Shift : Snapping
// R : Rotation
// (+) Shift : Snapping
// Y : Translation + Rotation
// (+) Shift : Snapping
// X : Change manipulation space
// Camera Following mode
// Choose one object from the list on the bottom right corner.

// if you want cancel the following mode, choose '--unfollowing--' menu from the list.

// Camera control
// Camera control is quite intuitive like a FPS game.

// W/S/A/D

// W or Mouse Scroll Up: Move Forward
// S or Mouse Scroll Down: Move Backward
// A: Move Left(sidestep left)
// D: Move Right(sidestep right)
// (+) Space: moving only on current plane(X,Z axis)
// Q/Z

// Q: Move upward
// Z: Move downward
// Mouse pointer with pressing "center button" on the mouse

// Moves screen view-port

		_helpContents= sb.ToString();
	}


	private void DrawHelpDialog()
	{
		GUI.skin.label.fontSize = labelFontSize;
		var dialogWidth = Screen.width * 0.8f;
		var dialogHeight = Screen.height * 0.8f;
		_rectDialog.x = Screen.width * 0.5f - dialogWidth * 0.5f;
		_rectDialog.y = Screen.height * 0.5f - dialogHeight * 0.5f;
		_rectDialog.width = dialogWidth;
		_rectDialog.height = dialogHeight;

		GUILayout.BeginArea(_rectDialog);
		GUILayout.TextArea(_helpContents);
		GUILayout.EndArea();
	}

	private void DrawHelpInfo()
	{
		GUI.skin.label.fontSize = labelFontSize;

		_rectHelpButton.x = Screen.width - buttonWidthHelp - textLeftMargin;
		GUILayout.BeginArea(_rectHelpButton);
		if (GUILayout.Button("Help(F1)"))
		{
			_popupHelpDialog = !_popupHelpDialog;
		}
		GUILayout.EndArea();

		GUI.skin.label.fontSize = (int)(labelFontSize * 0.8f);
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;

		var helpStatus = "Vertical Camera Lock " + ((_cameraControl.VerticalMovementLock)? "[X]":"[  ]");
		_rectHelpStatus.x = Screen.width -_rectHelpButton.width - helpStatusWidth - textLeftMargin;
		DrawShadow(_rectHelpStatus, helpStatus);
		GUILayout.BeginArea(_rectHelpStatus);
		GUILayout.Label(helpStatus);
		GUILayout.EndArea();
	}
}