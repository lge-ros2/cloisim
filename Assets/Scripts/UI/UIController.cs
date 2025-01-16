/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
	public enum CameraViewModeEnum
	{
		Perspective,
		Orthographic
	}

	private UIDocument _uiDocument = null;
	private VisualElement _rootVisualElement = null;
	private Toggle _toggleLockVerticalMoving = null;
	private TextField _scaleField = null;
	private Label _statusMessage = null;

	private const float CameraViewDistance = 30f;
	private const float ScaleFactorMin = 0.01f;
	private const float ScaleFactorMax = 10;
	private string _prevScaleFactorString = string.Empty;

	// Start is called before the first frame update
	void Start()
	{
		var objectSpawning = Main.ObjectSpawning;

		_toggleLockVerticalMoving = _rootVisualElement.Q<Toggle>("LockVerticalMoving");
		_toggleLockVerticalMoving.RegisterValueChangedCallback(x => Main.CameraControl.VerticalMovementLock = x.newValue);

		var buttonCameraView = _rootVisualElement.Q<Button>("CameraView");
		buttonCameraView.RegisterCallback<ClickEvent>(x => ShowCameraView());

		_scaleField = _rootVisualElement.Q<TextField>("ScaleField");
		var scaleFieldTextElem = _scaleField.Q<TextElement>();
		scaleFieldTextElem.style.unityTextAlign = TextAnchor.MiddleCenter;

		if (float.TryParse(_scaleField.text, out var scaleFactor))
		{
			objectSpawning.SetScaleFactor(scaleFactor);
		}
		_scaleField.RegisterCallback<FocusOutEvent>(OnFocusOutScaleField);
		_prevScaleFactorString = _scaleField.text;

		var buttonPropsBox = _rootVisualElement.Q<Button>("PropsBox");
		buttonPropsBox.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.BOX);

		var buttonPropsCylinder = _rootVisualElement.Q<Button>("PropsCylinder");
		buttonPropsCylinder.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.CYLINDER);

		var buttonPropsSphere = _rootVisualElement.Q<Button>("PropsSphere");
		buttonPropsSphere.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.SPHERE);

		_statusMessage = _rootVisualElement.Q<Label>("StatusMessage");
		ClearMessage();

		var buttonHelp = _rootVisualElement.Q<Button>("Help");
		buttonHelp.clickable.clicked += () => ShowHelp();

		var buttonSave = _rootVisualElement.Q<Button>("Save");
		buttonSave.clickable.clicked += () => SaveWorld();

		var buttonImport = _rootVisualElement.Q<Button>("Import");
		buttonImport.clickable.clicked += () => ShowModelList();

		var buttonHome = _rootVisualElement.Q<Button>("Home");
 		buttonHome.clickable.clicked += () => Main.CameraControl.StartCameraChange(Main.CameraInitPose);
		buttonHome.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonHome, Color.gray); });
		buttonHome.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonHome, Color.clear); });

		var buttonFront = _rootVisualElement.Q<Button>("Front");
		buttonFront.clickable.clicked += () => {
			var position = Vector3.forward * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonFront.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonFront, Color.gray); });
		buttonFront.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonFront, Color.clear); });

		var buttonLeft = _rootVisualElement.Q<Button>("Left");
		buttonLeft.clickable.clicked += () => {
			var position = Vector3.right * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonLeft.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonLeft, Color.gray); });
		buttonLeft.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonLeft, Color.clear); });

		var buttonBack = _rootVisualElement.Q<Button>("Back");
		buttonBack.clickable.clicked += () => {
			var position = Vector3.back * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonBack.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonBack, Color.gray); });
		buttonBack.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonBack, Color.clear); });

		var buttonRight = _rootVisualElement.Q<Button>("Right");
		buttonRight.clickable.clicked += () => {
			var position = Vector3.left * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonRight.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonRight, Color.gray); });
		buttonRight.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonRight, Color.clear); });

		var buttonTop = _rootVisualElement.Q<Button>("Top");
		buttonTop.clickable.clicked += () => {
			var position = Vector3.up * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonTop.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonTop, Color.gray); });
		buttonTop.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonTop, Color.clear); });

		var buttonBottom = _rootVisualElement.Q<Button>("Bottom");
		buttonBottom.clickable.clicked += () => {
			var position = Vector3.down * CameraViewDistance;
			var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
			Main.CameraControl.StartCameraChange(new Pose(position, rotation));
		};
		buttonBottom.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonBottom, Color.gray); });
		buttonBottom.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonBottom, Color.clear); });

		var camViewEnumField = _rootVisualElement.Q<EnumField>("CameraViewModeEnum");
		var enumFieldTextElem = camViewEnumField.Q<TextElement>();
		enumFieldTextElem.style.marginRight = 0;
		camViewEnumField.Init(CameraViewModeEnum.Perspective);
		camViewEnumField.SetEnabled(true);
		camViewEnumField.RegisterValueChangedCallback(evt =>
		{
			if (!evt.previousValue.Equals(evt.newValue))
			{
				// Debug.Log("Change Camera view mode: " + evt.newValue);
				if (evt.newValue.Equals(CameraViewModeEnum.Perspective))
				{
					Main.SetCameraPerspective();
				}
				else
				{
					Main.SetCameraOrthographic();
				}
			}
		});
	}

	private void ChangeBackground(ref Button button, in Color color)
	{
		button.style.backgroundColor = new StyleColor(color);
	}

	void LateUpdate()
	{
		if (Input.GetKeyUp(KeyCode.F1))
		{
			ShowHelp();
		}
		else if (Input.GetKeyUp(KeyCode.F3))
		{
			Main.ModelImporter.ToggleModelList();
		}
		else if (Input.GetKeyUp(KeyCode.Escape))
		{
			ShowHelp(false);
			ShowCameraView(false);
			Main.ModelImporter.ShowModelList(false);
		}
		else if (Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKeyUp(KeyCode.S))
			{
				// Debug.Log("Save World");
				SaveWorld();
			}
		}
	}

	void OnEnable()
	{
		_uiDocument = GetComponent<UIDocument>();
		_rootVisualElement = _uiDocument.rootVisualElement;

		UpdateVersionInfo();
	}

	public void ChangeCameraViewMode(in CameraViewModeEnum value)
	{
		var camViewEnumField = _rootVisualElement.Q<EnumField>("CameraViewModeEnum");
		camViewEnumField.SetValueWithoutNotify(value);
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

	private void ShowHelp(in bool open = true)
	{
		var helpDialogScrollView = _rootVisualElement.Q<ScrollView>("HelpDialog");

		if (!open || helpDialogScrollView.style.display == DisplayStyle.Flex)
		{
			helpDialogScrollView.style.display = DisplayStyle.None;
			Main.CameraControl.BlockMouseWheelControl(false);
		}
		else
		{
			helpDialogScrollView.style.display = DisplayStyle.Flex;
			Main.CameraControl.BlockMouseWheelControl(true);
		}
	}

	private void SaveWorld()
	{
		// Debug.Log("SaveWorld ButtonClicked");
		Main.Instance.SaveWorld();
	}

	private void ShowModelList()
	{
		// Debug.Log("ShowModelList ButtonClicked");
		Main.ModelImporter.ToggleModelList();
	}

	private void ShowCameraView(in bool open = true)
	{
		var cameraViewMenuVisElem = _rootVisualElement.Q<VisualElement>("CameraViewMenu");
		cameraViewMenuVisElem.style.visibility = (!open || cameraViewMenuVisElem.style.visibility == Visibility.Visible)? Visibility.Hidden : Visibility.Visible;
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
