/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class SimulationDisplay : MonoBehaviour
{
	[Header("Properties for Props menu")]
	private const float guiHeight = 25f;
	private const float toolbarWidth = 190f;
	// private readonly string[] toolbarStrings = new string[] { "Box", "Cylinder", "Sphere" };
	private string scaleFactorString = "0.5";
	// private int _toolbarSelected = 0;
	private string prevScaleFactorString;
	private bool checkScaleFactorFocused = false;
	private bool doCheckScaleFactorValue = false;
	private RectOffset zeroPadding;
	private bool isChangingScaleFactor = false;

	private void DrawPropsMenus()
	{
		style.fontSize = labelFontSize;
		style.wordWrap = true;
		style.padding = zeroPadding;
		style.alignment = TextAnchor.MiddleCenter;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;

		style.normal.textColor = Color.white;
		rectToolbar.x = Screen.width * 0.5f - toolbarWidth * 0.5f;
		// _toolbarSelected = GUI.Toolbar(rectToolbar, _toolbarSelected, toolbarStrings);

		var rectToolbarLabel = rectToolbar;
		rectToolbarLabel.x -= 45;
		rectToolbarLabel.width = 45;

		style.normal.textColor = Color.white;
		// DrawLabelWithShadow(rectToolbarLabel, "Props");

		var rectScaleLabel = rectToolbar;
		rectScaleLabel.x += (toolbarWidth + 10);
		rectScaleLabel.width = 35;
		style.normal.textColor = Color.white;
		// DrawLabelWithShadow(rectScaleLabel, "Size");

		var rectScale = rectScaleLabel;
		rectScale.x += (rectScaleLabel.width);
		rectScale.width = 45;
		// GUI.SetNextControlName("ScaleField");
		GUI.skin.textField.normal.textColor = Color.white;
		GUI.skin.textField.alignment = TextAnchor.MiddleCenter;
		// scaleFactorString = GUI.TextField(rectScale, scaleFactorString, 5);

		isChangingScaleFactor = (GUI.GetNameOfFocusedControl().CompareTo("ScaleField") == 0);

		rectHelpButton.x = rectScale.x + rectScale.width + textRightMargin * 5;

		if (checkScaleFactorFocused && !GUI.GetNameOfFocusedControl().Equals("ScaleField"))
		{
			doCheckScaleFactorValue = true;
			checkScaleFactorFocused = false;
			// Debug.Log("Focused out!!");
		}
		else if (!checkScaleFactorFocused && GUI.GetNameOfFocusedControl().Equals("ScaleField"))
		{
			// Debug.Log("Focused!!!");
			checkScaleFactorFocused = true;
			prevScaleFactorString = scaleFactorString;
		}

		if (doCheckScaleFactorValue)
		{
			// Debug.Log("Do check!! previous " + prevScaleFactorString);
			if (string.IsNullOrEmpty(scaleFactorString))
			{
				scaleFactorString = prevScaleFactorString;
			}
			else
			{
				if (float.TryParse(scaleFactorString, out var scaleFactor))
				{
					if (scaleFactor < 0.01f)
					{
						scaleFactorString = "0.01";
						scaleFactor = 0.01f;
					}
					else if (scaleFactor > 10f)
					{
						scaleFactorString = "10";
						scaleFactor = 10f;
					}
				}
				else
				{
					scaleFactorString = prevScaleFactorString;
				}

				Main.ObjectSpawning?.SetScaleFactor(scaleFactor);
			}

			doCheckScaleFactorValue = false;
		}

		// Main.ObjectSpawning?.SetPropType(_toolbarSelected);
	}
}