/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
	private UIDocument _uiDocument = null;
	private VisualElement _rootVisualElement = null;
	private Toggle _toggleLockVerticalMoving = null;
	private TextField _scaleField = null;
	private Label _statusMessage = null;

	private CameraControl _cameraControl = null;

	private const float ScaleFactorMin = 0.01f;
	private const float ScaleFactorMax = 10;
	private string _prevScaleFactorString = string.Empty;

	// Start is called before the first frame update
	void Start()
	{
		_cameraControl = Camera.main.GetComponent<CameraControl>();
		var objectSpawning = Main.ObjectSpawning;

		_toggleLockVerticalMoving = _rootVisualElement.Q<Toggle>("LockVerticalMoving");
		_toggleLockVerticalMoving.RegisterValueChangedCallback(x => _cameraControl.VerticalMovementLock = x.newValue);

		_scaleField = _rootVisualElement.Q<TextField>("ScaleField");
		if (float.TryParse(_scaleField.text, out var scaleFactor))
		{
			objectSpawning.SetScaleFactor(scaleFactor);
		}
		// _scaleField.RegisterValueChangedCallback(OnValueChangedScaleField);
		_scaleField.RegisterCallback<FocusOutEvent>(OnFocusOutScaleField);
		_prevScaleFactorString = _scaleField.text;

		var buttonPropsBox = _rootVisualElement.Q<Button>("PropsBox");
		buttonPropsBox.RegisterCallback<ClickEvent>(
			x => objectSpawning?.SetPropType(ObjectSpawning.PropsType.BOX));

		var buttonPropsCylinder = _rootVisualElement.Q<Button>("PropsCylinder");
		buttonPropsCylinder.RegisterCallback<ClickEvent>(
			x => objectSpawning?.SetPropType(ObjectSpawning.PropsType.CYLINDER));

		var buttonPropsSphere = _rootVisualElement.Q<Button>("PropsSphere");
		buttonPropsSphere.RegisterCallback<ClickEvent>(
			x => objectSpawning?.SetPropType(ObjectSpawning.PropsType.SPHERE));

		_statusMessage = _rootVisualElement.Q<Label>("StatusMessage");
		ClearMessage();

		var buttonHelp = _rootVisualElement.Q<Button>("Help");
		buttonHelp.RegisterCallback<ClickEvent>(x => ShowHelp());
	}

	void LateUpdate()
	{
		if (Input.GetKeyUp(KeyCode.F1))
		{
			ShowHelp();
		}
		else if (Input.GetKeyUp(KeyCode.Escape))
		{
			ShowHelp(true);
		}
	}

	void OnEnable()
	{
		_uiDocument = GetComponent<UIDocument>();
		_rootVisualElement = _uiDocument.rootVisualElement;

		UpdateVersionInfo();
	}

	private void UpdateVersionInfo()
	{
		var label = _rootVisualElement.Q<Label>("VersionInfo");
		label.text = Application.version;
	}

	public bool IsScaleFieldFocused()
	{
		// Debug.Log(_scaleField?.panel.focusController.focusedElement);
		return (_scaleField?.panel.focusController.focusedElement == _scaleField);
	}

	public void SetVerticalMovementLockToggle(in bool value)
	{
		_toggleLockVerticalMoving.value = value;
	}

	private void ShowHelp(in bool doClose = false)
	{
		var helpDialogScrollView = _rootVisualElement.Q<ScrollView>("HelpDialog");

		if (doClose || helpDialogScrollView.style.display == DisplayStyle.Flex)
		{
			helpDialogScrollView.style.display = DisplayStyle.None;
			_cameraControl.BlockMouseWheelControl(false);
		}
		else
		{
			helpDialogScrollView.style.display = DisplayStyle.Flex;
			_cameraControl.BlockMouseWheelControl(true);
		}
	}

	// private void OnValueChangedScaleField(ChangeEvent<string> evt)
	private void OnFocusOutScaleField(FocusOutEvent evt)
	{
		var textField = evt.target as TextField;
		var scaleFactor = 0f;
		var scaleFactorString = textField.text;

		if (string.IsNullOrEmpty(scaleFactorString))
		{
			scaleFactorString = _prevScaleFactorString;
		}

		if (float.TryParse(scaleFactorString, out scaleFactor))
		{
			if (scaleFactor < ScaleFactorMin)
			{
				_prevScaleFactorString = ScaleFactorMin.ToString();
				_scaleField.value = _prevScaleFactorString;
				scaleFactor = ScaleFactorMin;
			}
			else if (scaleFactor > ScaleFactorMax)
			{
				_prevScaleFactorString = ScaleFactorMax.ToString();
				_scaleField.value = _prevScaleFactorString;
				scaleFactor = ScaleFactorMax;
			}
			else
			{
				_prevScaleFactorString = scaleFactor.ToString();
			}
		}
		else
		{
			scaleFactor = float.Parse(_prevScaleFactorString);
			_scaleField.value = _prevScaleFactorString;
			Debug.Log("Invalid scale factor: " + scaleFactorString);
		}

		Main.ObjectSpawning?.SetScaleFactor(scaleFactor);
	}

	public void ClearMessage()
	{
		SetStatusMessage(string.Empty, Color.clear);
	}

	public void SetStatusMessage(in string message, in Color color)
	{
		if (_statusMessage != null)
		{
			_statusMessage.style.color = color;
			_statusMessage.text = message;
		}
	}

	public void SetEventMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.green);
	}

	public void SetDebugMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.blue);
	}

	public void SetInfoMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.gray);
	}

	public void SetErrorMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.red);
	}

	public void SetWarningMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.yellow);
	}
}
