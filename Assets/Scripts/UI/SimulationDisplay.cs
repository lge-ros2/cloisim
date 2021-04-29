/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;

public class SimulationDisplay : MonoBehaviour
{
	private Clock clock = null;
	private ObjectSpawning _objectSpawning = null;
	private string eventMessage = string.Empty;
	private StringBuilder sbTimInfo = new StringBuilder();
	private string _fpsString = string.Empty;

	[Header("fps")]
	private const float fpsUpdatePeriod = 0.5f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

	[Header("GUI properties")]
	private const int labelFontSize = 15;

	private const int textLeftMargin = 10;
	private const int textTopMargin = 10;
	private const int textHeight = 23;

	private const int textWidthFps = 75;
	private const int textWidthVersion = 50;
	private const int textWidthSimulationInfo = 550;
	private const int textWidthEvent = 800;

	private Color logMessageColor = Color.red;

	// private const int labelFontSize = 14;
	private const int TextWidth = 45;
	private Rect labelRect  = new Rect(Screen.width / 2 - TextWidth / 2, 10, TextWidth, 22);

	private const float guiHeight = 25f;
	private const float topMargin = 10f;
	private const float toolbarWidth = 190f;
	private string[] toolbarStrings = new string[] { "Box", "Cylinder", "Sphere" };

	private string scaleFactorString = "0.5";
	private int toolbarSelected = 0;

	[Header("Rect")]
	private Rect _rectVersion;
	private Rect _rectSimulationInfo;
	private Rect _rectFPS;
	private Rect _rectLogMessage;

	private string prevScaleFactorString;
	private bool checkScaleFactorFocused = false;
	private bool doCheckScaleFactorValue = false;

	// private Texture2D consoleBackground;

	// Start is called before the first frame update
	void Awake()
	{
		var coreObject = GameObject.Find("Core");
		_objectSpawning = coreObject.GetComponent<ObjectSpawning>();
		clock = DeviceHelper.GetGlobalClock();

		// consoleBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
    	// consoleBackground.SetPixel(0, 0, new Color(0, 0, 0, 0.25f));
    	// consoleBackground.Apply(); // not sure if this is necessary

		_rectVersion = new Rect(textLeftMargin, textTopMargin, textWidthVersion, textHeight);
		_rectSimulationInfo = new Rect(textLeftMargin, Screen.height - textHeight - textTopMargin, textWidthSimulationInfo, textHeight);
		_rectFPS = new Rect(textWidthSimulationInfo + _rectSimulationInfo.x,  Screen.height - textHeight - textTopMargin, textWidthFps, textHeight);
		// _rectFPS = new Rect(Screen.width - textWidthFps - textLeftMargin, textTopMargin, textWidthFps, textHeight);
		_rectLogMessage = new Rect(textLeftMargin, Screen.height - (textHeight*2) - textTopMargin, textWidthEvent, textHeight);
	}

	void Update()
	{
		CalculateFPS();
	}

	void LateUpdate()
	{
		_fpsString = "FPS [" + GetBoldText(Mathf.Round(fps).ToString("F1")) + "]";
	}

	public void ClearLogMessage()
	{
		eventMessage = string.Empty;
	}

	public void SetEventMessage(in string value)
	{
		logMessageColor = Color.green;
		eventMessage = value;
	}

	public void SetErrorMessage(in string value)
	{
		logMessageColor = Color.red;
		eventMessage = value;
	}

	private void CalculateFPS()
	{
		frameCount++;
		dT += Time.unscaledDeltaTime;
		if (dT > fpsUpdatePeriod)
		{
			fps = frameCount / dT;
			dT -= fpsUpdatePeriod;
			frameCount = 0;
		}
	}

	private string GetTimeInfoString()
	{
		var simTime = (clock == null) ? Time.time : clock.SimTime;
		var realTime = (clock == null) ? Time.realtimeSinceStartup : clock.RealTime;

		var simTs = TimeSpan.FromSeconds(simTime);
		var realTs = TimeSpan.FromSeconds(realTime);
		var diffTs1 = realTs - simTs;

		var currentSimTime = GetBoldText(simTs.ToString(@"d\:hh\:mm\:ss\.fff"));
		var currentRealTime = GetBoldText(realTs.ToString(@"d\:hh\:mm\:ss\.fff"));
		var diffRealSimTime = GetBoldText(diffTs1.ToString(@"d\:hh\:mm\:ss\.fff"));

		sbTimInfo.Clear();
		sbTimInfo.AppendFormat("Time: Simulation [{0}] | Real [{1}] | Real-Sim [{2}]", currentSimTime, currentRealTime, diffRealSimTime);
		return sbTimInfo.ToString();
	}

	private string GetBoldText(in string value)
	{
		return ("<b>" + value + "</b>");
	}

	private void DrawShadow(in Rect rect, in string value)
	{
		var prevColor = GUI.skin.label.normal.textColor;

		GUI.skin.label.normal.textColor = new Color(0, 0, 0, 0.34f);
		var rectShadow = rect;
		rectShadow.x += 1;
		rectShadow.y += 1;
		GUI.Label(rectShadow, value);

		GUI.skin.label.normal.textColor = prevColor;
	}

	private void DrawText()
	{
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.label.wordWrap = true;

		// version info
		var versionString = GetBoldText(Application.version);
		DrawShadow(_rectVersion, versionString);
		GUI.skin.label.normal.textColor = Color.green;
		GUI.Label(_rectVersion, versionString);

		// Simulation time info
		var simulationInfo = GetTimeInfoString();
		_rectSimulationInfo.y = Screen.height - textHeight - textTopMargin;
		DrawShadow(_rectSimulationInfo, simulationInfo);
		GUI.skin.label.normal.textColor = Color.black;
		// GUI.skin.label.normal.background = consoleBackground;
		GUI.Label(_rectSimulationInfo, simulationInfo);

		// fps info
		_rectFPS.y = Screen.height - textHeight - textTopMargin;
		DrawShadow(_rectFPS, _fpsString);
		GUI.skin.label.normal.textColor = Color.blue;
		GUI.Label(_rectFPS, _fpsString);

		GUI.skin.label.normal.background = null;

		// logging: error message or event message
		var originLabelSkin = GUI.skin.label;

		GUI.skin.label.wordWrap = true;
		GUI.skin.label.clipping = TextClipping.Overflow;
		_rectLogMessage.y = Screen.height - (textHeight*2) - textTopMargin;
		DrawShadow(_rectLogMessage, eventMessage);
		GUI.skin.label.normal.textColor = logMessageColor;
		GUI.Label(_rectLogMessage, eventMessage);

		GUI.skin.label = originLabelSkin;
	}

	private void DrawPropsMenus()
	{
		GUI.skin.label.alignment = TextAnchor.MiddleRight;
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		var centerPointX = Screen.width / 2;

		var rectToolbar = new Rect(centerPointX - toolbarWidth / 2, topMargin, toolbarWidth, guiHeight);
		GUI.skin.label.normal.textColor = Color.white;
		toolbarSelected = GUI.Toolbar(rectToolbar, toolbarSelected, toolbarStrings);

		var rectToolbarLabel = rectToolbar;
		rectToolbarLabel.x -= 45;
		rectToolbarLabel.width = 45;

		DrawShadow(rectToolbarLabel, "Props: ");
		GUI.skin.label.normal.textColor = Color.white;
		GUI.Label(rectToolbarLabel, "Props: ");

		var rectScaleLabel = rectToolbar;
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

		_objectSpawning.SetPropType(toolbarSelected);
		_objectSpawning.SetScaleFactor(scaleFactorString);
	}

	private void DrawHelpers()
	{

	}

	void OnGUI()
	{
		var originLabelColor = GUI.skin.label.normal.textColor;

		DrawText();

		DrawPropsMenus();

		DrawHelpers();

		GUI.skin.label.normal.textColor = originLabelColor;
	}
}