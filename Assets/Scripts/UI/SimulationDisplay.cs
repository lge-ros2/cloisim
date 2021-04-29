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
	private CameraControl _cameraControl = null;
	private string eventMessage = string.Empty;
	private StringBuilder sbTimInfo = new StringBuilder();
	private string _fpsString = string.Empty;

	[Header("fps")]
	private const float fpsUpdatePeriod = 0.5f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

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

	[Header("Properties for Props menu")]
	private const float guiHeight = 25f;
	private const float toolbarWidth = 190f;
	private string[] toolbarStrings = new string[] { "Box", "Cylinder", "Sphere" };
	private string scaleFactorString = "0.5";
	private int toolbarSelected = 0;
	private string prevScaleFactorString;
	private bool checkScaleFactorFocused = false;
	private bool doCheckScaleFactorValue = false;
	private Texture2D textureBackground;

	[Header("Help dialog")]
	private String _helpContents;
	private const int buttonWidthHelp = 65;
	private const int helpStatusWidth = buttonWidthHelp * 2;

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

	void Update()
	{
		CalculateFPS();
	}

	void LateUpdate()
	{
		_fpsString = "FPS [" + GetBoldText(Mathf.Round(fps).ToString("F1")) + "]";
	}

	private void UpdateHelpContents()
	{
		var sb = new StringBuilder();
		sb.AppendLine("How to manipulate");

// 		General
// Ctrl + R : Reset simulation Ctrl + Shift + R : simulation Full reset like restart

// Object spawning
// Choose one of model on the top of screen.[Box], [Cylinder], [Sphere]

// Pressing 'Left-Ctrl' Key,

// Spawning:
// (+) Click the left mouse button on the pointer
// Remove:
// (+) Click the right mouse button on the object already spawned
// Object Control
// After select object you want,

// T : Translation
// (+) Shift : Snapping
// R : Rotation
// (+) Shift : Snapping
// Y : Translation + Rotation
// (+) Shift : Snapping
// X : Change manipulation space
// Camera Following mode
// Choose one object from the list on the bottom right corner.

// if you want cancel the following mode, choose '--unfollowing--' menu from the list.

// Camera control
// Camera control is quite intuitive like a FPS game.

// W/S/A/D

// W or Mouse Scroll Up: Move Forward
// S or Mouse Scroll Down: Move Backward
// A: Move Left(sidestep left)
// D: Move Right(sidestep right)
// (+) Space: moving only on current plane(X,Z axis)
// Q/Z

// Q: Move upward
// Z: Move downward
// Mouse pointer with pressing "center button" on the mouse

// Moves screen view-port

		_helpContents= sb.ToString();
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

		GUI.skin.label.normal.background = null;

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

	private void DrawPropsMenus()
	{
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

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

	private void DrawHelpDialog()
	{
		GUI.skin.label.fontSize = labelFontSize;
		var dialogWidth = Screen.width * 0.8f;
		var dialogHeight = Screen.height * 0.8f;
		_rectDialog.x = Screen.width * 0.5f - dialogWidth * 0.5f;
		_rectDialog.y = Screen.height * 0.5f - dialogHeight * 0.5f;
		_rectDialog.width = dialogWidth;
		_rectDialog.height = dialogHeight;

		GUILayout.BeginArea(_rectDialog);
		GUILayout.TextArea(_helpContents);
		GUILayout.EndArea();
	}

	private void DrawHelpInfo()
	{
		GUI.skin.label.fontSize = labelFontSize;

		_rectHelpButton.x = Screen.width - buttonWidthHelp - textLeftMargin;
		GUILayout.BeginArea(_rectHelpButton);
		if (GUILayout.Button("Help(F1)"))
		{
			_popupHelpDialog = !_popupHelpDialog;
		}
		GUILayout.EndArea();

		GUI.skin.label.fontSize = (int)(labelFontSize * 0.8f);
		GUI.skin.label.alignment = TextAnchor.MiddleLeft;

		var helpStatus = "Vertical Camera Lock " + ((_cameraControl.VerticalMovementLock)? "[X]":"[  ]");
		_rectHelpStatus.x = Screen.width -_rectHelpButton.width - helpStatusWidth - textLeftMargin;
		DrawShadow(_rectHelpStatus, helpStatus);
		GUILayout.BeginArea(_rectHelpStatus);
		GUILayout.Label(helpStatus);
		GUILayout.EndArea();
	}

	private bool _popupHelpDialog = false;

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
			Debug.Log("Show Help dialog");
			DrawHelpDialog();
		}

		GUI.skin.label.normal.textColor = originLabelColor;
	}
}