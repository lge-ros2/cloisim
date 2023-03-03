/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UnityEngine;

[DefaultExecutionOrder(50)]
public partial class SimulationDisplay : MonoBehaviour
{
	private Clock clock = null;
	private ObjectSpawning objectSpawning = null;
	private CameraControl cameraControl = null;

	private StringBuilder eventMessage = new StringBuilder();

	[Header("GUI properties")]
	private const int labelFontSize = 14;
	private const int textFontSize = 12;
	private const int topMargin = 6;
	private const int bottomMargin = 15;
	private const int textLeftMargin = 10;
	private const int textRightMargin = 10;
	private const int textHeight = 20;

	private const int textWidthFps = 70;
	private const int TextWidthPointInfo = 300;
	private const int textWidthVersion = 50;
	private const int textWidthSimulationInfo = 500;
	private const int textWidthOptionInfo = 250;
	private const int textWidthEvent = 800;

	private Color logMessageColor = Color.black;

	[Header("Rect")]
	private Rect rectVersion;
	private Rect rectOption;
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

		if (Main.CoreObject != null)
		{
			objectSpawning = Main.CoreObject.GetComponent<ObjectSpawning>();
		}
		else
		{
			Debug.LogError("Main.CoreObject is not ready!!");
		}

		cameraControl = GetComponentInChildren<CameraControl>();
		clock = DeviceHelper.GetGlobalClock();

		textureBackground = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
		textureBackground.SetPixel(0, 0, new Color(0, 0, 0, 0.75f));
		textureBackground.Apply(); // not sure if this is necessary

		rectVersion = new Rect(textLeftMargin, topMargin, textWidthVersion, textHeight);
		rectOption = new Rect(textWidthVersion + textRightMargin, topMargin, textWidthOptionInfo, textHeight);
		rectSimulationInfo = new Rect(textLeftMargin, Screen.height - textHeight - bottomMargin, textWidthSimulationInfo, textHeight);
		rectFps = new Rect(rectSimulationInfo.width + rectSimulationInfo.x, Screen.height - textHeight - bottomMargin, textWidthFps, textHeight);
		rectPointInfo = new Rect(rectFps.width + rectFps.x, Screen.height - textHeight - bottomMargin, TextWidthPointInfo, textHeight);
		rectLogMessage = new Rect(textLeftMargin, Screen.height - (textHeight * 2) - bottomMargin, textWidthEvent, textHeight);

		rectToolbar = new Rect(0, topMargin, toolbarWidth, guiHeight);

		rectDialog = new Rect();
		rectHelpButton = new Rect(Screen.width - buttonWidthHelp - textLeftMargin, topMargin, buttonWidthHelp, guiHeight);
		rectHelpStatus = new Rect(Screen.width - rectHelpButton.width - helpStatusWidth - textLeftMargin, topMargin, helpStatusWidth, textHeight * 1.1f);

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
		ClearLogMessage();
		logMessageColor = Color.green;
		eventMessage.Append(value);
	}

	public void SetDebugMessage(in string value)
	{
		ClearLogMessage();
		logMessageColor = Color.blue;
		eventMessage.Append(value);
	}

	public void SetInfoMessage(in string value)
	{
		ClearLogMessage();
		logMessageColor = Color.gray;
		eventMessage.Append(value);
	}

	public void SetErrorMessage(in string value)
	{
		ClearLogMessage();
		logMessageColor = Color.red;
		eventMessage.Append(value);
	}

	public void SetWarningMessage(in string value)
	{
		ClearLogMessage();
		logMessageColor = Color.yellow;
		eventMessage.Append(value);
	}

	private void DrawLabelWithShadow(in Rect rect, in string value)
	{
		var styleShadow = new GUIStyle(style);
		styleShadow.normal.textColor = new Color(0.1f, 0.1f, 0.1f, 0.70f);

		var rectShadow = rect;
		rectShadow.x += 1;
		rectShadow.y += 1;
		GUI.Label(rectShadow, value, styleShadow);
		GUI.Label(rect, value, style);
	}

	private void DrawText()
	{
		style.alignment = TextAnchor.MiddleLeft;
		style.normal.textColor = new Color(0, 0, 0, 0.80f);
		style.fontStyle = FontStyle.Bold;
		style.fontSize = textFontSize;
		style.wordWrap = true;
		style.clipping = TextClipping.Overflow;
		style.stretchHeight = false;
		style.stretchWidth = false;

		// version info
		style.normal.textColor = Color.green;
		DrawLabelWithShadow(rectVersion, versionInfo);

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
				cameraControl.BlockMouseWheelControl();
			}
			else if (keyCode.CompareTo(KeyCode.Escape) == 0)
			{
				popupHelpDialog = false;
				cameraControl.UnBlockMouseWheelControl();
			}
		}

		if (popupHelpDialog)
		{
			// Debug.Log("Show Help dialog");
			DrawHelpDialog();
		}
	}
}