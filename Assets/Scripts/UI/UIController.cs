using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour
{
	private UIDocument _uiDocument = null;
	private VisualElement _rootVisualElement = null;
	private Toggle _toggleLockVerticalMoving = null;
	private CameraControl _cameraControl = null;

	// Start is called before the first frame update
	void Start()
	{
		_cameraControl = Camera.main.GetComponent<CameraControl>();
		_toggleLockVerticalMoving = _rootVisualElement.Q<Toggle>("LockVerticalMoving");
		_toggleLockVerticalMoving.RegisterCallback<MouseUpEvent>(MouseUpHandleEvent);
	}

	void LateUpdate()
	{
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

	private void MouseUpHandleEvent(MouseUpEvent e)
	{
		_cameraControl.VerticalMovementLock = _toggleLockVerticalMoving.value;
	}

	public void SetVerticalMovementLockToggle(in bool value)
	{
		_toggleLockVerticalMoving.value = value;
	}
}
