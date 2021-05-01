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
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;
		GUI.skin.label.padding = new RectOffset(0, 0, 0, 0);

		GUI.skin.label.normal.textColor = Color.white;
		_rectToolbar.x = Screen.width * 0.5f - toolbarWidth * 0.5f;
		toolbarSelected = GUI.Toolbar(_rectToolbar, toolbarSelected, toolbarStrings);

		var rectToolbarLabel = _rectToolbar;
		rectToolbarLabel.x -= 45;
		rectToolbarLabel.width = 45;

		DrawShadow(rectToolbarLabel, "Props: ");
		GUI.skin.label.normal.textColor = Color.white;
		GUI.Label(rectToolbarLabel, "Props: ");

		var rectScaleLabel = _rectToolbar;
		rectScaleLabel.x += (toolbarWidth + 7);
		rectScaleLabel.width = 50;
		DrawShadow(rectScaleLabel, "Scale: ");
		GUI.skin.label.normal.textColor = Color.white;
		GUI.Label(rectScaleLabel, "Scale: ");

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

		_objectSpawning?.SetPropType(toolbarSelected);
		_objectSpawning?.SetScaleFactor(scaleFactorString);
	}
}