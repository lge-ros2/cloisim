	/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UnityEngine;

public partial class SimulationDisplay : MonoBehaviour
{
	[Header("Help dialog")]
	private GUIContent helpContents = new GUIContent();
	private float helpContentsHeight;
	private const int buttonWidthHelp = 65;
	private const int helpStatusWidth = buttonWidthHelp * 2;
	private bool popupHelpDialog = false;
	private Vector2 scrollPosition = Vector2.zero;

	private RectOffset padding;
	private Rect viewRect;

	private void UpdateHelpContents()
	{
		var sb = new StringBuilder();
		sb.AppendLine("<b>How to manipulate</b>");
		sb.AppendLine("");
		sb.AppendLine(" - General");
		sb.AppendLine("");
		sb.AppendLine("    <b>Ctrl + R</b>: Reset simulation");
		sb.AppendLine("    <b>Ctrl + Shift + R</b>: simulation Full reset like restart");
		sb.AppendLine("");
		sb.AppendLine("");
		sb.AppendLine(" - Object Control");
		sb.AppendLine("");
		sb.AppendLine("    Selection/Deselection: Mouse <b>Left click</b>");
		sb.AppendLine("      Multiple Selection/Deseletion: + <b>Left Shift</b>");
		sb.AppendLine("");
		sb.AppendLine("    After select object you want, press the key as following.");
		sb.AppendLine("      <b>T</b> : Translation");
		sb.AppendLine("      <b>R</b> : Rotation");
		sb.AppendLine("      <b>Y</b> : Translation + Rotation");
		sb.AppendLine("      <b>X</b> : Change manipulation space");
		sb.AppendLine("      <b>X</b> : Change manipulation space");
		sb.AppendLine("      Move <b>Axis Arrow</b> or <b>plane handle</b>: move the object");
		sb.AppendLine("        Snapping(optional): + <b>Shift</b>");
		sb.AppendLine("");
		sb.AppendLine("");
		sb.AppendLine("- Object spawning: choose one of model on the top of screen. [Box], [Cylinder], [Sphere] ");
		sb.AppendLine("");
		sb.AppendLine("    Pressing <b>Left Ctrl</b> key and,");
		sb.AppendLine("      Spawning: + Mouse <b>Left Click</b> on the cursor");
		sb.AppendLine("      Remove: + Mouse <b>Right Click</b> on the object already spawned");
		sb.AppendLine("    Remove: Press <b>delete</b> key after select the object.");
		sb.AppendLine("");
		sb.AppendLine("");
		sb.AppendLine(" - Camera following mode");
		sb.AppendLine("");
		sb.AppendLine("    <b>Choose one object from the list</b> on the bottom right corner.");
		sb.AppendLine("    if you want cancel the following mode, choose '--unfollowing--' menu from the list.");
		sb.AppendLine("");
		sb.AppendLine("");
		sb.AppendLine(" - Camera Control: Camera control is quite intuitive like a FPS game (W/S/A/D/Q/Z)");
		sb.AppendLine("");
		sb.AppendLine("    <b>W</b> or <b>Mouse Scroll Up</b>: Move Forward");
		sb.AppendLine("    <b>S</b> or <b>Mouse Scroll Down</b>: Move Backward");
		sb.AppendLine("    <b>A</b>: Move Left(sidestep left)");
		sb.AppendLine("    <b>D</b>: Move Right(sidestep right)");
		sb.AppendLine("    <b>Q</b>: Move upward");
 		sb.AppendLine("    <b>Z</b>: Move downward");
		sb.AppendLine("    <b>Space</b>: moving only on current plane(X, Z axis). it's toggling.");
		sb.AppendLine("");
		sb.AppendLine("");
		sb.AppendLine(" - Change camera view-port(screen)");
		sb.AppendLine("    Moving cursor pressing <b>center button</b> or <b>rigth button</b> on the mouse");
		sb.AppendLine("");
		sb.AppendLine("            @ Let me know if you are stuck in any trouble :)");
		sb.AppendLine("");

		helpContentsHeight = 58 * labelFontSize;
		helpContents.text = sb.ToString();

		viewRect = new Rect(0, 0, rectDialog.width - 20, helpContentsHeight);
	}

	private void DrawHelpDialog()
	{
		var dialogWidth = Screen.width * 0.8f;
		var dialogHeight = Screen.height * 0.85f;
		rectDialog.x = Screen.width * 0.5f - dialogWidth * 0.5f;
		rectDialog.y = Screen.height * 0.5f - dialogHeight * 0.5f;
		rectDialog.width = dialogWidth;
		rectDialog.height = dialogHeight;

		style.alignment = TextAnchor.UpperLeft;
		style.padding = padding;
		style.fontSize = labelFontSize;
		style.richText = true;
		style.wordWrap = true;
		style.normal.textColor = Color.white;
		style.normal.background = textureBackground;

		viewRect.width = rectDialog.width - 20;
		scrollPosition = GUI.BeginScrollView(rectDialog, scrollPosition, viewRect, false, true);
		GUI.Label(new Rect(0, 0, rectDialog.width - 16, helpContentsHeight), helpContents, style);
		GUI.EndScrollView();

		style.padding = zeroPadding;
		style.normal.background = null;
	}

	private void DrawHelpInfo()
	{
		style.fontSize = labelFontSize;
		style.wordWrap = true;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;

		rectHelpButton.x = Screen.width - buttonWidthHelp - textLeftMargin;
		GUI.skin.button.normal.textColor = Color.white;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		if (GUI.Button(rectHelpButton, "Help(F1)"))
		{
			popupHelpDialog = !popupHelpDialog;
		}

		style.fontSize = (int)(labelFontSize * 0.8f);
		style.alignment = TextAnchor.MiddleLeft;
		style.normal.textColor = Color.white;
		var helpStatus = "Vertical Camera Lock " + ((cameraControl.VerticalMovementLock)? "[X]":"[  ]");
		rectHelpStatus.x = Screen.width -rectHelpButton.width - helpStatusWidth - textLeftMargin;
		DrawLabelWithShadow(rectHelpStatus, helpStatus, style);
	}
}
