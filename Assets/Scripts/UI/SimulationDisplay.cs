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
	private Clock clock = null;
	private ObjectSpawning _objectSpawning = null;
	private CameraControl _cameraControl = null;
	private string eventMessage = string.Empty;
	private StringBuilder sbTimInfo = new StringBuilder();


	[Header("GUI properties")]
	private const int labelFontSize = 14;
	private const int topMargin = 6;
	private const int textLeftMargin = 10;
	private const int textHeight = 19;

	private const int textWidthFps = 75;
	private const int textWidthVersion = 50;
	private const int textWidthSimulationInfo = 550;
	private const int textWidthEvent = 800;

	private Color logMessageColor = Color.red;

	[Header("Rect")]
	private Rect _rectVersion;
	private Rect _rectSimulationInfo;
	private Rect _rectFPS;
	private Rect _rectLogMessage;
	private Rect _rectDialog;
	private Rect _rectToolbar;
	private Rect _rectHelpButton;
	private Rect _rectHelpStatus;

	private Texture2D textureBackground;

	// Start is called before the first frame update
	void Awake()
	{
		var coreObject = GameObject.Find("Core");
		_objectSpawning = coreObject.GetComponent<ObjectSpawning>();
		_cameraControl = GetComponentInChildren<CameraControl>();
		clock = DeviceHelper.GetGlobalClock();

		textureBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
		textureBackground.SetPixel(0, 0, new Color(0, 0, 0, 0.25f));
		textureBackground.Apply(); // not sure if this is necessary

		_rectVersion = new Rect(textLeftMargin, topMargin, textWidthVersion, textHeight);
		_rectSimulationInfo = new Rect(textLeftMargin, Screen.height - textHeight - topMargin, textWidthSimulationInfo, textHeight);
		_rectFPS = new Rect(_rectSimulationInfo.width + _rectSimulationInfo.x,  Screen.height - textHeight - topMargin, textWidthFps, textHeight);
		_rectLogMessage = new Rect(textLeftMargin, Screen.height - (textHeight*2) - topMargin, textWidthEvent, textHeight);

		_rectDialog = new Rect();

		_rectToolbar = new Rect(0, topMargin, toolbarWidth, guiHeight);

		_rectHelpButton = new Rect(Screen.width - buttonWidthHelp - textLeftMargin, topMargin, buttonWidthHelp, guiHeight);

		_rectHelpStatus = new Rect(Screen.width -_rectHelpButton.width - helpStatusWidth - textLeftMargin, topMargin, helpStatusWidth, textHeight * 1.1f);

		UpdateHelpContents();
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
		// GUI.skin.label.normal.background = textureBackground;
		GUI.Label(_rectVersion, versionString);

		// Simulation time info
		var simulationInfo = GetTimeInfoString();
		_rectSimulationInfo.y = Screen.height - textHeight - topMargin;
		DrawShadow(_rectSimulationInfo, simulationInfo);
		GUI.skin.label.normal.textColor = Color.black;
		GUI.Label(_rectSimulationInfo, simulationInfo);

		// fps info
		_rectFPS.y = Screen.height - textHeight - topMargin;
		DrawShadow(_rectFPS, _fpsString);
		GUI.skin.label.normal.textColor = Color.blue;
		GUI.Label(_rectFPS, _fpsString);

		// logging: error message or event message
		var originLabelSkin = GUI.skin.label;

		GUI.skin.label.wordWrap = true;
		GUI.skin.label.clipping = TextClipping.Overflow;
		_rectLogMessage.y = Screen.height - (textHeight*2) - topMargin;
		DrawShadow(_rectLogMessage, eventMessage);
		GUI.skin.label.normal.textColor = logMessageColor;
		GUI.Label(_rectLogMessage, eventMessage);

		GUI.skin.label = originLabelSkin;
	}

	void OnGUI()
	{
		var originLabelColor = GUI.skin.label.normal.textColor;

		DrawText();

		DrawPropsMenus();

		DrawHelpInfo();

		if (Event.current.type.Equals(EventType.KeyUp))
		{
			if (Event.current.keyCode.CompareTo(KeyCode.F1) == 0)
			{
				_popupHelpDialog = !_popupHelpDialog;
			}
			else if (Event.current.keyCode.CompareTo(KeyCode.Escape) == 0)
			{
				_popupHelpDialog = false;
			}
		}

		if (_popupHelpDialog)
		{
			// Debug.Log("Show Help dialog");
			DrawHelpDialog();
		}

		GUI.skin.label.normal.textColor = originLabelColor;
	}
}