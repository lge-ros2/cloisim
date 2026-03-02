/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
#if !UNITY_SERVER
using UnityEngine.UIElements;
#endif
#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine.Rendering.HighDefinition;
#endif

public class UIController : MonoBehaviour
{
	public enum CameraViewModeEnum
	{
		Perspective,
		Orthographic
	}

#if !UNITY_SERVER
	private UIDocument _uiDocument = null;
	private VisualElement _rootVisualElement = null;
	private Toggle _toggleLockVerticalMoving = null;
	private TextField _scaleField = null;
	private Label _statusMessage = null;
	private Button _recordSave = null;

	private const float CameraViewDistance = 30f;
	private const float ScaleFactorMin = 0.01f;
	private const float ScaleFactorMax = 10;
	private string _prevScaleFactorString = string.Empty;
#endif

	void Awake()
	{
#if !UNITY_SERVER
		_uiDocument = GetComponent<UIDocument>();
		if (_uiDocument != null) _rootVisualElement = _uiDocument.rootVisualElement;
#endif
	}

	void Start()
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var objectSpawning = Main.ObjectSpawning;

		_toggleLockVerticalMoving = _rootVisualElement.Q<Toggle>("LockVerticalMoving");
		if (_toggleLockVerticalMoving != null) _toggleLockVerticalMoving.RegisterValueChangedCallback(x => Main.CameraControl.VerticalMovementLock = x.newValue);

		var buttonCameraView = _rootVisualElement.Q<Button>("CameraView");
		if (buttonCameraView != null) buttonCameraView.RegisterCallback<ClickEvent>(x => ShowCameraView());

		var buttonRenderSettings = _rootVisualElement.Q<Button>("RenderSettings");
		if (buttonRenderSettings != null) buttonRenderSettings.RegisterCallback<ClickEvent>(x => ShowRenderSettings());

		SetupRenderSettingsToggles();

		_scaleField = _rootVisualElement.Q<TextField>("ScaleField");
		if (_scaleField != null)
		{
			var scaleFieldTextElem = _scaleField.Q<TextElement>();
			scaleFieldTextElem.style.unityTextAlign = TextAnchor.MiddleCenter;

			if (float.TryParse(_scaleField.text, out var scaleFactor))
			{
				objectSpawning.SetScaleFactor(scaleFactor);
			}
			_scaleField.RegisterCallback<FocusOutEvent>(OnFocusOutScaleField);
			_prevScaleFactorString = _scaleField.text;
		}

		var buttonPropsBox = _rootVisualElement.Q<Button>("PropsBox");
		if (buttonPropsBox != null) buttonPropsBox.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.BOX);

		var buttonPropsCylinder = _rootVisualElement.Q<Button>("PropsCylinder");
		if (buttonPropsCylinder != null) buttonPropsCylinder.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.CYLINDER);

		var buttonPropsSphere = _rootVisualElement.Q<Button>("PropsSphere");
		if (buttonPropsSphere != null) buttonPropsSphere.clickable.clicked += () => objectSpawning?.SetPropType(ObjectSpawning.PropsType.SPHERE);

		_statusMessage = _rootVisualElement.Q<Label>("StatusMessage");
		ClearMessage();

		var buttonHelp = _rootVisualElement.Q<Button>("Help");
		if (buttonHelp != null) buttonHelp.clickable.clicked += () => ShowHelp();

		_recordSave = _rootVisualElement.Q<Button>("Record");
		if (_recordSave != null) _recordSave.clickable.clicked += () => {
			var recording = Main.Instance.ToggleRecord();
			OnRecordClicked(recording);
		};

		var buttonSave = _rootVisualElement.Q<Button>("Save");
		if (buttonSave != null) buttonSave.clickable.clicked += () => Main.Instance.SaveWorld();

		var buttonImport = _rootVisualElement.Q<Button>("Import");
		if (buttonImport != null) buttonImport.clickable.clicked += () => Main.ModelImporter.ToggleModelList();

		var buttonHome = _rootVisualElement.Q<Button>("Home");
		if (buttonHome != null)
		{
			buttonHome.clickable.clicked += () => Main.CameraControl.StartCameraChange(Main.CameraInitPose);
			buttonHome.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonHome, Color.gray); });
			buttonHome.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonHome, Color.clear); });
		}

		var buttonFront = _rootVisualElement.Q<Button>("Front");
		if (buttonFront != null)
		{
			buttonFront.clickable.clicked += () => {
				var position = Vector3.forward * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonFront.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonFront, Color.gray); });
			buttonFront.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonFront, Color.clear); });
		}

		var buttonLeft = _rootVisualElement.Q<Button>("Left");
		if (buttonLeft != null)
		{
			buttonLeft.clickable.clicked += () => {
				var position = Vector3.right * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonLeft.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonLeft, Color.gray); });
			buttonLeft.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonLeft, Color.clear); });
		}

		var buttonBack = _rootVisualElement.Q<Button>("Back");
		if (buttonBack != null)
		{
			buttonBack.clickable.clicked += () => {
				var position = Vector3.back * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonBack.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonBack, Color.gray); });
			buttonBack.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonBack, Color.clear); });
		}

		var buttonRight = _rootVisualElement.Q<Button>("Right");
		if (buttonRight != null)
		{
			buttonRight.clickable.clicked += () => {
				var position = Vector3.left * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonRight.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonRight, Color.gray); });
			buttonRight.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonRight, Color.clear); });
		}

		var buttonTop = _rootVisualElement.Q<Button>("Top");
		if (buttonTop != null)
		{
			buttonTop.clickable.clicked += () => {
				var position = Vector3.up * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonTop.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonTop, Color.gray); });
			buttonTop.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonTop, Color.clear); });
		}

		var buttonBottom = _rootVisualElement.Q<Button>("Bottom");
		if (buttonBottom != null)
		{
			buttonBottom.clickable.clicked += () => {
				var position = Vector3.down * CameraViewDistance;
				var rotation = Quaternion.LookRotation(Main.CoreObject.transform.position - position);
				Main.CameraControl.StartCameraChange(new Pose(position, rotation));
			};
			buttonBottom.RegisterCallback<MouseEnterEvent>(delegate { ChangeBackground(ref buttonBottom, Color.gray); });
			buttonBottom.RegisterCallback<MouseLeaveEvent>(delegate { ChangeBackground(ref buttonBottom, Color.clear); });
		}

		var camViewEnumField = _rootVisualElement.Q<EnumField>("CameraViewModeEnum");
		if (camViewEnumField != null)
		{
			var enumFieldTextElem = camViewEnumField.Q<TextElement>();
			enumFieldTextElem.style.marginRight = 0;
			camViewEnumField.Init(CameraViewModeEnum.Perspective);
			camViewEnumField.SetEnabled(true);
			camViewEnumField.RegisterValueChangedCallback(evt =>
			{
				if (!evt.previousValue.Equals(evt.newValue))
				{
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

		UpdateWebServiceInfo();
		UpdateVersionInfo();
#endif
	}

#if !UNITY_SERVER
	private void ChangeBackground(ref Button button, in Color color)
	{
		button.style.backgroundColor = new StyleColor(color);
	}
#endif

	void LateUpdate()
	{
#if !UNITY_SERVER
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
			ShowRenderSettings(false);
			Main.ModelImporter.ShowModelList(false);
		}
		else if (Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKeyUp(KeyCode.S))
			{
				Main.Instance.SaveWorld();
			}
		}
#endif
	}

	public void OnRecordClicked(bool enable)
	{
#if !UNITY_SERVER
		_recordSave?.EnableInClassList("recording", enable);
#endif
	}

	public void ChangeCameraViewMode(in CameraViewModeEnum value)
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var camViewEnumField = _rootVisualElement.Q<EnumField>("CameraViewModeEnum");
		camViewEnumField?.SetValueWithoutNotify(value);
#endif
	}

	private void UpdateWebServiceInfo()
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var label = _rootVisualElement.Q<Label>("WebServiceInfo");
		if (label != null) label.text = Main.SimulationService.ServicePort.ToString();
#endif
	}

	private void UpdateVersionInfo()
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var label = _rootVisualElement.Q<Label>("VersionInfo");
		if (label != null) label.text = Application.version;
#endif
	}

	public bool IsScaleFieldFocused()
	{
#if !UNITY_SERVER
		return (_scaleField?.panel.focusController.focusedElement == _scaleField);
#else
		return false;
#endif
	}

	public void SetVerticalMovementLockToggle(in bool value)
	{
#if !UNITY_SERVER
		if (_toggleLockVerticalMoving != null) _toggleLockVerticalMoving.value = value;
#endif
	}

	private void ShowHelp(in bool open = true)
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var helpDialogScrollView = _rootVisualElement.Q<ScrollView>("HelpDialog");
		if (helpDialogScrollView == null) return;

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
#endif
	}

	private void ShowCameraView(in bool open = true)
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var cameraViewMenuVisElem = _rootVisualElement.Q<VisualElement>("CameraViewMenu");
		if (cameraViewMenuVisElem == null) return;
		cameraViewMenuVisElem.style.visibility = (!open || cameraViewMenuVisElem.style.visibility == Visibility.Visible)? Visibility.Hidden : Visibility.Visible;
#endif
	}

	private void ShowRenderSettings(in bool open = true)
	{
#if !UNITY_SERVER
		if (_rootVisualElement == null) return;
		var renderSettingsMenu = _rootVisualElement.Q<VisualElement>("RenderSettingsMenu");
		if (renderSettingsMenu == null) return;

		var isVisible = (!open || renderSettingsMenu.style.visibility == Visibility.Visible);
		renderSettingsMenu.style.visibility = isVisible ? Visibility.Hidden : Visibility.Visible;
		Main.CameraControl.BlockMouseWheelControl(!isVisible);
#endif
	}

#if !UNITY_SERVER
	private static readonly (string toggleName, FrameSettingsField field)[] RenderFeatureMap = new[]
	{
		("RenderToggle_ShadowMaps", FrameSettingsField.ShadowMaps),
		("RenderToggle_SSAO", FrameSettingsField.SSAO),
		("RenderToggle_SSR", FrameSettingsField.SSR),
		("RenderToggle_Volumetrics", FrameSettingsField.Volumetrics),
		("RenderToggle_AtmosphericScattering", FrameSettingsField.AtmosphericScattering),
		("RenderToggle_ContactShadows", FrameSettingsField.ContactShadows),
		("RenderToggle_ScreenSpaceShadows", FrameSettingsField.ScreenSpaceShadows),
		("RenderToggle_MotionVectors", FrameSettingsField.MotionVectors),
		("RenderToggle_Decals", FrameSettingsField.Decals),
		("RenderToggle_SubsurfaceScattering", FrameSettingsField.SubsurfaceScattering),
		("RenderToggle_Refraction", FrameSettingsField.Refraction),
	};

	private void SetupRenderSettingsToggles()
	{
		if (_rootVisualElement == null) return;

		// Initialize shared render quality defaults
		// (GUI mode: full HDRP quality; headless: expensive features off)
		RenderQualityManager.Initialize();

		// Apply defaults to GUI camera immediately
		var hdCamData = Camera.main?.GetComponent<HDAdditionalCameraData>();
		if (hdCamData != null)
		{
			RenderQualityManager.ApplyTo(hdCamData);
		}

		foreach (var (toggleName, field) in RenderFeatureMap)
		{
			var toggle = _rootVisualElement.Q<Toggle>(toggleName);
			if (toggle == null) continue;

			// Sync toggle state from RenderQualityManager's current overrides.
			// null (no override) means HDRP default = enabled → show toggle as ON.
			var featureState = RenderQualityManager.IsEnabled(field);
			toggle.SetValueWithoutNotify(featureState ?? true);

			var capturedField = field;
			toggle.RegisterValueChangedCallback(evt => SetHDRPFeature(capturedField, evt.newValue));
		}

		// ── Post-Processing & Photorealistic feature toggles ──
		SetupPhotorealisticToggles();
	}

	// Maps UXML toggle names to PhotorealisticSetup.Feature enum values.
	private static readonly (string toggleName, PhotorealisticSetup.Feature feature)[] PhotoFeatureMap = new[]
	{
		// Post Processing
		("PhotoToggle_Bloom",            PhotorealisticSetup.Feature.Bloom),
		("PhotoToggle_Tonemapping",      PhotorealisticSetup.Feature.Tonemapping),
		("PhotoToggle_AutoExposure",     PhotorealisticSetup.Feature.AutoExposure),
		// Photorealistic
		("PhotoToggle_SSR",              PhotorealisticSetup.Feature.SSR),
		("PhotoToggle_VolumetricFog",    PhotorealisticSetup.Feature.VolumetricFog),
		("PhotoToggle_ColorGrading",     PhotorealisticSetup.Feature.ColorGrading),
		("PhotoToggle_WhiteBalance",     PhotorealisticSetup.Feature.WhiteBalance),
		("PhotoToggle_Vignette",         PhotorealisticSetup.Feature.Vignette),
		("PhotoToggle_IndirectLighting", PhotorealisticSetup.Feature.IndirectLighting),
	};

	private void SetupPhotorealisticToggles()
	{
		if (_rootVisualElement == null) return;
		var setup = PhotorealisticSetup.Instance;

		// Master toggle — enables/disables the entire photorealistic volume
		var masterToggle = _rootVisualElement.Q<Toggle>("PhotoToggle_Master");
		if (masterToggle != null)
		{
			masterToggle.SetValueWithoutNotify(setup != null && setup.IsActive);
			masterToggle.RegisterValueChangedCallback(evt =>
			{
				var ps = PhotorealisticSetup.Instance;
				if (ps != null) ps.SetEnabled(evt.newValue);
			});
		}

		// Individual feature toggles
		foreach (var (toggleName, feature) in PhotoFeatureMap)
		{
			var toggle = _rootVisualElement.Q<Toggle>(toggleName);
			if (toggle == null) continue;

			toggle.SetValueWithoutNotify(setup != null && setup.IsFeatureEnabled(feature));

			var capturedFeature = feature;
			toggle.RegisterValueChangedCallback(evt =>
			{
				var ps = PhotorealisticSetup.Instance;
				if (ps != null) ps.SetFeatureEnabled(capturedFeature, evt.newValue);
			});
		}
	}

	private void SetHDRPFeature(FrameSettingsField field, bool enabled)
	{
		// Update shared state and propagate to GUI camera + all sensor cameras
		RenderQualityManager.SetFeature(field, enabled);
	}
#endif

#if !UNITY_SERVER
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
#endif

	public void ClearMessage()
	{
		SetStatusMessage(string.Empty, Color.clear);
	}

	public void SetStatusMessage(in string message, in Color color)
	{
#if !UNITY_SERVER
		if (_statusMessage != null)
		{
			_statusMessage.style.color = color;
			_statusMessage.text = message;
		}
#else
        Debug.Log($"UI Status Message: {message}");
#endif
	}

	public void SetEventMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.cyan);
	}

	public void SetDebugMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.green);
	}

	public void SetInfoMessage(in string value)
	{
		ClearMessage();
		SetStatusMessage(value, Color.blue);
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
