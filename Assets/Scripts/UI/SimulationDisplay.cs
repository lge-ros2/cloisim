	/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;

[DefaultExecutionOrder(50)]
public partial class SimulationDisplay : MonoBehaviour
{
	private Clock clock = null;
	private ObjectSpawning objectSpawning = null;
	private CameraControl cameraControl = null;
	private StringBuilder eventMessage = new StringBuilder();
	private StringBuilder sbTimeInfo = new StringBuilder();

	[Header("GUI properties")]
	private const int labelFontSize = 14;
	private const int topMargin = 6;
	private const int bottomMargin = 10;
	private const int textLeftMargin = 10;
	private const int textHeight = 19;

	private const int textWidthFps = 75;
	private const int textWidthVersion = 50;
	private const int textWidthSimulationInfo = 550;
	private const int textWidthEvent = 800;

	private Color logMessageColor = Color.red;

	[Header("Rect")]
	private Rect rectVersion;
	private Rect rectSimulationInfo;
	private Rect rectFps;
	private Rect rectLogMessage;
	private Rect rectDialog;
	private Rect rectToolbar;
	private Rect rectHelpButton;
	private Rect rectHelpStatus;

	private GUIStyle style;
	private Texture2D textureBackground;

	// Start is called before the first frame update
	void Awake()
	{
		var coreObject = Main.CoreObject;
		objectSpawning = coreObject.GetComponent<ObjectSpawning>();
		cameraControl = GetComponentInChildren<CameraControl>();
		clock = DeviceHelper.GetGlobalClock();

		textureBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
		textureBackground.SetPixel(0, 0, new Color(0, 0, 0, 0.75f));
		textureBackground.Apply(); // not sure if this is necessary

		rectVersion = new Rect(textLeftMargin, topMargin, textWidthVersion, textHeight);
		rectSimulationInfo = new Rect(textLeftMargin, Screen.height - textHeight - bottomMargin, textWidthSimulationInfo, textHeight);
		rectFps = new Rect(rectSimulationInfo.width + rectSimulationInfo.x,  Screen.height - textHeight - bottomMargin, textWidthFps, textHeight);
		rectLogMessage = new Rect(textLeftMargin, Screen.height - (textHeight * 2) - bottomMargin, textWidthEvent, textHeight);

		rectToolbar = new Rect(0, topMargin, toolbarWidth, guiHeight);

		rectDialog = new Rect();
		rectHelpButton = new Rect(Screen.width - buttonWidthHelp - textLeftMargin, topMargin, buttonWidthHelp, guiHeight);
		rectHelpStatus = new Rect(Screen.width -rectHelpButton.width - helpStatusWidth - textLeftMargin, topMargin, helpStatusWidth, textHeight * 1.1f);

		style = new GUIStyle();

		padding = new RectOffset(30, 30, 30, 30);
		zeroPadding = new RectOffset(0, 0, 0, 0);

		InitPropsMenu();
		UpdateHelpContents();
	}

	public void ClearLogMessage()
	{
		eventMessage.Clear();
	}

	public void SetEventMessage(in string value)
	{
		logMessageColor = Color.green;
		eventMessage.AppendLine(value);
	}

	public void SetErrorMessage(in string value)
	{
		logMessageColor = Color.red;
		eventMessage.AppendLine(value);
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

		sbTimeInfo.Clear();
		sbTimeInfo.AppendFormat("Time: Simulation [{0}] | Real [{1}] | Real-Sim [{2}]", currentSimTime, currentRealTime, diffRealSimTime);
		return sbTimeInfo.ToString();
	}

	private string GetBoldText(in string value)
	{
		return ("<b>" + value + "</b>");
	}

	private void DrawLabelWithShadow(in Rect rect, in string value, GUIStyle style)
	{
		var styleShadow = new GUIStyle(style);
		styleShadow.normal.textColor = new Color(0, 0, 0, 0.85f);

		var rectShadow = rect;
		rectShadow.x += 1;
		rectShadow.y += 1;
		GUI.Label(rectShadow, value, styleShadow);
		GUI.Label(rect, value, style);
	}

	private void DrawText()
	{
		style.alignment = TextAnchor.MiddleLeft;
		style.normal.textColor = new Color(0, 0, 0, 0.85f);
		style.fontSize = labelFontSize;
		style.wordWrap = true;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;

		// version info
		var versionString = GetBoldText(Application.version);
		style.normal.textColor = Color.green;
		DrawLabelWithShadow(rectVersion, versionString, style);

		// Simulation time info
		var simulationInfo = GetTimeInfoString();
		rectSimulationInfo.y = Screen.height - textHeight - bottomMargin;
		style.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1);
		DrawLabelWithShadow(rectSimulationInfo, simulationInfo, style);

		DrawFPSText(style);

		// logging: error message or event message
		rectLogMessage.y = Screen.height - (textHeight * 2) - bottomMargin;
		style.normal.textColor = logMessageColor;
		DrawLabelWithShadow(rectLogMessage, eventMessage.ToString(), style);
	}

	void OnGUI()
	{
		DrawText();

		DrawPropsMenus();

		DrawHelpInfo();

		if (Event.current.type.Equals(EventType.KeyUp))
		{
			var keyCode = Event.current.keyCode;
			if (keyCode.CompareTo(KeyCode.F1) == 0)
			{
				popupHelpDialog = !popupHelpDialog;
			}
			else if (keyCode.CompareTo(KeyCode.Escape) == 0)
			{
				popupHelpDialog = false;
			}
		}

		if (popupHelpDialog)
		{
			// Debug.Log("Show Help dialog");
			DrawHelpDialog();
			cameraControl.blockMouseWheelControl = true;
		}
		else
		{
			cameraControl.blockMouseWheelControl = false;
		}
	}
}