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
	[Header("Properties for Props menu")]
	private const float guiHeight = 25f;
	private const float toolbarWidth = 190f;
	private string[] toolbarStrings = new string[] { "Box", "Cylinder", "Sphere" };
	private string scaleFactorString = "0.5";
	private int toolbarSelected = 0;
	private string prevScaleFactorString;
	private bool checkScaleFactorFocused = false;
	private bool doCheckScaleFactorValue = false;

	private void DrawPropsMenus()
	{
		var style = new GUIStyle();
		style.fontSize = labelFontSize;
		style.wordWrap = true;
		style.padding = new RectOffset(0, 0, 0, 0);
		style.alignment = TextAnchor.MiddleCenter;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;


		style.normal.textColor = Color.white;
		rectToolbar.x = Screen.width * 0.5f - toolbarWidth * 0.5f;
		toolbarSelected = GUI.Toolbar(rectToolbar, toolbarSelected, toolbarStrings);

		var rectToolbarLabel = rectToolbar;
		rectToolbarLabel.x -= 45;
		rectToolbarLabel.width = 45;

		style.normal.textColor = Color.white;
		DrawLabelWithShadow(rectToolbarLabel, "Props: ", style);

		var rectScaleLabel = rectToolbar;
		rectScaleLabel.x += (toolbarWidth + 7);
		rectScaleLabel.width = 50;
		style.normal.textColor = Color.white;
		DrawLabelWithShadow(rectScaleLabel, "Scale: ", style);

		var rectScale = rectScaleLabel;
		rectScale.x += 50;
		rectScale.width = 40;
		GUI.SetNextControlName("ScaleField");
		GUI.skin.textField.normal.textColor = Color.white;
		GUI.skin.textField.alignment = TextAnchor.MiddleCenter;
		scaleFactorString = GUI.TextField(rectScale, scaleFactorString, 5);

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
			if (string.IsNullOrEmpty(scaleFactorString) )
			{
				scaleFactorString = prevScaleFactorString;
			}
			else
			{
				if (float.TryParse(scaleFactorString, out var scaleFactor))
				{
					if (scaleFactor < 0.1f)
					{
						scaleFactorString = "0.1";
					}
					else if (scaleFactor > 5f)
					{
						scaleFactorString = "5";
					}
				}
				else
				{
					scaleFactorString = prevScaleFactorString;
				}
			}

			doCheckScaleFactorValue = false;
		}

		objectSpawning?.SetPropType(toolbarSelected);
		objectSpawning?.SetScaleFactor(scaleFactorString);
	}
}