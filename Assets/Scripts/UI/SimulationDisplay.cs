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
	private StringBuilder sbTimeInfo = new StringBuilder(78);
	private StringBuilder sbPointInfo = new StringBuilder(38);

	private Vector3 pointInfo = Vector3.zero;

	[Header("GUI properties")]
	private const int labelFontSize = 14;
	private const int topMargin = 6;
	private const int bottomMargin = 10;
	private const int textLeftMargin = 10;
	private const int textHeight = 19;

	private const int textWidthFps = 70;
	private const int TextWidthPointInfo = 300;
	private const int textWidthVersion = 50;
	private const int textWidthSimulationInfo = 490;
	private const int textWidthEvent = 800;

	private Color logMessageColor = Color.red;

	[Header("Rect")]
	private Rect rectVersion;
	private Rect rectSimulationInfo;
	private Rect rectFps;
	private Rect rectPointInfo;
	private Rect rectLogMessage;
	private Rect rectDialog;
	private Rect rectToolbar;
	private Rect rectHelpButton;
	private Rect rectHelpStatus;

	private GUIStyle style;
	private Texture2D textureBackground;

	[Header("Data")]
	private string versionInfo;

	// Start is called before the first frame update
	void Awake()
	{
		versionInfo = Application.version;

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
		rectPointInfo = new Rect(rectFps.width + rectFps.x,  Screen.height - textHeight - bottomMargin, TextWidthPointInfo, textHeight);
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
		var currentSimTime = (clock == null) ? string.Empty : clock.ToHMS().SimTime.ToString();
		var currentRealTime = (clock == null) ? string.Empty : clock.ToHMS().RealTime.ToString();
		var diffRealSimTime = (clock == null) ? string.Empty : clock.ToHMS().DiffTime.ToString();

		sbTimeInfo.Clear();
		sbTimeInfo.Append("Time: Sim [");
		sbTimeInfo.Append(currentSimTime);
		sbTimeInfo.Append("] Real[");
		sbTimeInfo.Append(currentRealTime);
		sbTimeInfo.Append("] Real-Sim [");
		sbTimeInfo.Append(diffRealSimTime);
		sbTimeInfo.Append("]");
		return sbTimeInfo.ToString();
	}

	public void SetPointInfo(in Vector3 point)
	{
		this.pointInfo = point;
	}

	private void DrawPointInfoText()
	{
		rectPointInfo.y = Screen.height - textHeight - bottomMargin;
		style.fontStyle = FontStyle.Bold;
		style.normal.textColor = new Color(1.0f, 0.93f, 0.0f, 1);
		sbPointInfo.Clear();
		sbPointInfo.Append("HitPoint (");
		sbPointInfo.Append(pointInfo.x);
		sbPointInfo.Append(", ");
		sbPointInfo.Append(pointInfo.y);
		sbPointInfo.Append(", ");
		sbPointInfo.Append(pointInfo.z);
		sbPointInfo.Append(")");
		DrawLabelWithShadow(rectPointInfo, sbPointInfo.ToString());
	}

	private void DrawTimeInfoText()
	{
		var simulationInfo = GetTimeInfoString();
		rectSimulationInfo.y = Screen.height - textHeight - bottomMargin;
		style.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1);
		DrawLabelWithShadow(rectSimulationInfo, simulationInfo);
	}

	private void DrawLabelWithShadow(in Rect rect, in string value)
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
		style.fontStyle = FontStyle.Bold;
		style.fontSize = labelFontSize;
		style.wordWrap = true;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;

		// version info
		style.normal.textColor = Color.green;
		DrawLabelWithShadow(rectVersion, versionInfo);

		DrawTimeInfoText();

		DrawFPSText();

		DrawPointInfoText();

		// logging: error message or event message
		rectLogMessage.y = Screen.height - (textHeight * 2) - bottomMargin;
		style.normal.textColor = logMessageColor;
		DrawLabelWithShadow(rectLogMessage, eventMessage.ToString());
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