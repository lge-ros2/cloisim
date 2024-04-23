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
	private StringBuilder _sbOption = new StringBuilder(45);
	private GUIContent helpContents = new GUIContent();
	private float helpContentsHeight;
	private const int buttonWidthHelp = 45;
	private bool popupHelpDialog = false;
	private Vector2 scrollPosition = Vector2.zero;

	private RectOffset padding;
	private Rect viewRect;

	private void UpdateHelpContents()
	{
		var sb = new StringBuilder();
		sb.AppendLine("<b>How to manipulate</b>");
		sb.AppendLine(string.Empty);
		sb.AppendLine(" - General");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    <b>Ctrl + R</b>: Reset simulation");
		sb.AppendLine("    <b>Ctrl + Shift + R</b>: simulation Full reset like restart");
		sb.AppendLine("    <b>Ctrl + C</b>: Copy Selected object (supports only single object)");
		sb.AppendLine("    <b>Ctrl + V</b>: Past copied object");
		sb.AppendLine("    <b>Ctrl + S</b>: Save current world");
		sb.AppendLine(string.Empty);
		sb.AppendLine(string.Empty);
		sb.AppendLine(" - Object Control");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    Selection/Deselection: Mouse <b>Left click</b>");
		sb.AppendLine("    Select static object: + <b>Left Alt</b>");
		sb.AppendLine("      Multiple(Adding) Selection/Deseletion: + <b>Left Shift</b>");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    After select object you want, press the key as following.");
		sb.AppendLine("      <b>T</b>: Translation");
		sb.AppendLine("      <b>R</b>: Rotation");
		sb.AppendLine("      <b>Y</b>: Translation + Rotation");
		sb.AppendLine("      <b>X</b>: Change manipulation space");
		sb.AppendLine("      <b>X</b>: Change manipulation space");
		sb.AppendLine("      Move <b>Axis Arrow</b> or <b>plane handle</b>: move the object");
		sb.AppendLine("        Snapping(optional): + <b>Shift</b>");
		sb.AppendLine(string.Empty);
		sb.AppendLine(string.Empty);
		sb.AppendLine("- Object spawning: choose one of model on the top of screen. [Box], [Cylinder], [Sphere]");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    Choose Prop by Number Key");
		sb.AppendLine("      <b>1</b>: [Box]");
		sb.AppendLine("      <b>1</b>: [Cylinder]");
		sb.AppendLine("      <b>2</b>: [Sphere]");
		sb.AppendLine("    Pressing <b>Left Ctrl</b> key and,");
		sb.AppendLine("      Spawning: + Mouse <b>Left Click</b> on the cursor");
		sb.AppendLine("      Remove: + Mouse <b>Right Click</b> on the object already spawned");
		sb.AppendLine("    Remove: Press <b>delete</b> key after select the object.");
		sb.AppendLine(string.Empty);
		sb.AppendLine(string.Empty);
		sb.AppendLine(" - Camera following mode");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    <b>Choose one object from the list</b> on the bottom right corner.");
		sb.AppendLine("    if you want cancel the following mode, choose '--unfollowing--' menu from the list.");
		sb.AppendLine(string.Empty);
		sb.AppendLine(string.Empty);
		sb.AppendLine(" - Camera Control: Camera control is quite intuitive like a FPS game (W/S/A/D + R/F)");
		sb.AppendLine(string.Empty);
		sb.AppendLine("    <b>W</b> or <b>Mouse Scroll Up</b>: Move Forward");
		sb.AppendLine("    <b>S</b> or <b>Mouse Scroll Down</b>: Move Backward");
		sb.AppendLine("    <b>A</b>: Move Left(sidestep left)");
		sb.AppendLine("    <b>D</b>: Move Right(sidestep right)");
		sb.AppendLine("    <b>Q</b>: Turn Left");
		sb.AppendLine("    <b>E</b>: Turn Right");
		sb.AppendLine("    <b>R</b>: Move upward");
		sb.AppendLine("    <b>F</b>: Move downward");
		sb.AppendLine("    <b>Space</b>: moving only on current plane(XZ plane). It's toggling.");
		sb.AppendLine(string.Empty);
		sb.AppendLine(string.Empty);
		sb.AppendLine(" - Change camera view-port(screen)");
		sb.AppendLine("    Moving cursor pressing <b>center button</b> or <b>rigth button</b> on the mouse");
		sb.AppendLine(string.Empty);
		sb.AppendLine("            @ Let me know if you are stuck in any trouble :)");
		sb.AppendLine(string.Empty);

		var lines = sb.ToString().Split(new string[] { System.Environment.NewLine }, System.StringSplitOptions.None).Length;
		helpContentsHeight = (int)(lines * labelFontSize * 1.2);
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

		GUI.skin.button.normal.textColor = Color.white;
		GUI.skin.button.alignment = TextAnchor.MiddleCenter;
		if (GUI.Button(rectHelpButton, "Help"))
		{
			popupHelpDialog = !popupHelpDialog;
		}

		style.fontSize = (int)(labelFontSize * 0.8f);
		style.alignment = TextAnchor.MiddleLeft;
		style.normal.textColor = Color.white;

		_sbOption.Clear();
		_sbOption.Append("Lock Vertical Moving");
		var checkValue = (cameraControl != null && cameraControl.VerticalMovementLock) ? "[X]" : "[  ]";
		_sbOption.Append(checkValue);
		// var helpStatusMsg2 = "\nStatic Object Selectable(O) " + ((cameraControl.VerticalMovementLock)? "[V]":"[  ]");
		DrawLabelWithShadow(rectOption, _sbOption.ToString());
	}
}