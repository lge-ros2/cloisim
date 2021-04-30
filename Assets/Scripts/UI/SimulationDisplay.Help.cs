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
	private GUIContent _helpContents = new GUIContent();
	private float _helpContentsHeight;
	private const int buttonWidthHelp = 65;
	private const int helpStatusWidth = buttonWidthHelp * 2;
	private bool _popupHelpDialog = false;
	private Vector2 _scrollPosition = Vector2.zero;

	private void UpdateHelpContents()
	{
		var sb = new StringBuilder();
		sb.AppendLine("<b>How to manipulate</b>");
		sb.AppendLine("");
		sb.AppendLine("- General");
		sb.AppendLine("  <b>Ctrl + R</b>: Reset simulation");
		sb.AppendLine("  <b>Ctrl + Shift + R</b>: simulation Full reset like restart");
		sb.AppendLine("");
		sb.AppendLine("- Object spawning: choose one of model on the top of screen. [Box], [Cylinder], [Sphere] ");
		sb.AppendLine("  Pressing <b>Left Ctrl</b> key and,");
		sb.AppendLine("    Spawning: <b>+ Click the left mouse button</b> on the cursor");
		sb.AppendLine("    Remove: <b>+ Click the right mouse button</b> on the object already spawned");
		sb.AppendLine("");
		sb.AppendLine("- Object Control");
		sb.AppendLine("After select object you want,");
		sb.AppendLine("press the key as following.");
		sb.AppendLine("  Snapping: <b>+ Shift</b>");
		sb.AppendLine("  <b>T</b> : Translation");
		sb.AppendLine("  <b>R</b> : Rotation");
		sb.AppendLine("  <b>Y</b> : Translation + Rotation");
		sb.AppendLine("  <b>X</b> : Change manipulation space");
		sb.AppendLine("");
		sb.AppendLine("- Camera following mode");
		sb.AppendLine("  <b>Choose one object from the list</b> on the bottom right corner.");
		sb.AppendLine("  if you want cancel the following mode, choose '--unfollowing--' menu from the list.");
		sb.AppendLine("");
		sb.AppendLine("- Camera Control: Camera control is quite intuitive like a FPS game (W/S/A/D/Q/Z)");
		sb.AppendLine("");
		sb.AppendLine("  <b>W</b> or <b>Mouse Scroll Up</b>: Move Forward");
		sb.AppendLine("  <b>S</b> or <b>Mouse Scroll Down</b>: Move Backward");
		sb.AppendLine("  <b>A</b>: Move Left(sidestep left)");
		sb.AppendLine("  <b>D</b>: Move Right(sidestep right)");
		sb.AppendLine("  <b>Q</b>: Move upward");
 		sb.AppendLine("  <b>Z</b>: Move downward");
		sb.AppendLine("");
		sb.AppendLine("  <b>Space</b>: moving only on current plane(X, Z axis). it's toggling.");
		sb.AppendLine("");
		sb.AppendLine("- Change camera view-port(screen)");
		sb.AppendLine("  Moving cursor pressing <b>center button</b> or <b>rigth button</b> on the mouse");
		sb.AppendLine("");

		_helpContentsHeight = 50 * labelFontSize;
		_helpContents.text = sb.ToString();
	}

	private void DrawHelpDialog()
	{
		var dialogWidth = Screen.width * 0.8f;
		var dialogHeight = Screen.height * 0.8f;
		_rectDialog.x = Screen.width * 0.5f - dialogWidth * 0.5f;
		_rectDialog.y = Screen.height * 0.5f - dialogHeight * 0.5f;
		_rectDialog.width = dialogWidth;
		_rectDialog.height = dialogHeight;

		var style = new GUIStyle();
		style.alignment = TextAnchor.UpperLeft;
		style.padding = new RectOffset(30, 30, 20, 20);
		style.fontSize = labelFontSize;
		style.richText = true;
		style.wordWrap = true;
		style.normal.background = textureBackground;

		_scrollPosition = GUI.BeginScrollView(_rectDialog, _scrollPosition, new Rect(0, 0, _rectDialog.width - 20, _helpContentsHeight), false, true);
		GUI.Label(new Rect(0, 0, _rectDialog.width - 16, _helpContentsHeight), _helpContents, style);
		GUI.EndScrollView();
	}

	private void DrawHelpInfo()
	{
		_rectHelpButton.x = Screen.width - buttonWidthHelp - textLeftMargin;
		if (GUI.Button(_rectHelpButton, "Help(F1)"))
		{
			_popupHelpDialog = !_popupHelpDialog;
		}

		GUI.skin.label.fontSize = (int)(labelFontSize * 0.8f);
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;

		var helpStatus = "Vertical Camera Lock " + ((_cameraControl.VerticalMovementLock)? "[X]":"[  ]");
		_rectHelpStatus.x = Screen.width -_rectHelpButton.width - helpStatusWidth - textLeftMargin;
		DrawShadow(_rectHelpStatus, helpStatus);
		GUI.Label(_rectHelpStatus, helpStatus);
	}
}