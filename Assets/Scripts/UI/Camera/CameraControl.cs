/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public abstract class CameraControl : MonoBehaviour
{
	/*
		Writen by Windexglow 11-13-10.  Use it, edit it, steal it I don't care.
		Converted to C# 27-02-13 - no credit wanted.
		Simple flycam I made, since I couldn't find any others made public.
		Made simple to use (drag and drop, done) for regular keyboard layout
		wasd : basic movement
		shift : Makes camera accelerate
		space : Moves camera on X and Z axis only.  So camera doesn't gain any height
	*/

	protected bool _blockControl = false;

	protected bool _blockMouseWheelControl = false;

	protected bool _verticalMovementLock = false;

	protected bool _terminateMoving = false;

	private UIController _uiController = null;

	public bool VerticalMovementLock
	{
		set => _verticalMovementLock = value;
		get => _verticalMovementLock;
	}

	protected const float MoveSmoothSpeed = .0025f;

	[SerializeField]
	protected float _mainSpeed = 3.5f; // regular speed

	[SerializeField]
	protected float _shiftAdd = 10.0f; // multiplied by how long shift is held.  Basically running

	[SerializeField]
	protected float _maxShift = 60.0f; // Maximum speed when holding shift

	[SerializeField]
	protected float _camSens = 0.1f; // How sensitive it with mouse

	[SerializeField]
	protected float _edgeWidth = 100.0f;

	[SerializeField]
	protected float _edgeSens = 0.02f;

	[SerializeField]
	protected float _edgeSensMax = 1.0f;

	[SerializeField]
	protected float _wheelMoveAmp = 50f;

	[SerializeField]
	protected float _wheelMoveOrthoSize = 0.25f;

	[SerializeField]
	protected float _angleStep = 1.5f;

	protected Vector3 _lastMouse = Vector3.zero; // kind of in the middle of the screen, rather than at the top (play)
	protected float _totalRun = 1.0f;
	protected float _edgeSensAccumlated = 0.0f;

	protected int _targetLayerMask = 0;

	private Coroutine _movingCoroutine = null;

	void Awake()
	{
		_targetLayerMask = LayerMask.GetMask("Default");
		_uiController = GetUIController();
		// Debug.Log(_uiController);
	}

	void LateUpdate()
	{
		if (_blockControl || PIDTunerWindow.IsEditing)
		{
			GetUIController()?.UpdateCameraKeyOverlay(CameraKeyOverlayInput.None);
			return;
		}

		if (Keyboard.current[Key.Space].wasReleasedThisFrame)
		{
			LockVerticalMovement();
		}

		if (Keyboard.current[Key.LeftCtrl].isPressed && Keyboard.current[Key.Z].wasReleasedThisFrame)
		{
			StartCameraChange(Main.CameraInitPose);
		}

		GetUIController()?.UpdateCameraKeyOverlay(GetActiveCameraKeyOverlayInputs());

		HandleMouseControl();

		// Keyboard commands for Translation
		var targetPosByKey = HandleKeyboardCommands();

		targetPosByKey *= Time.deltaTime;

		var newPosition = transform.position;
		if (_verticalMovementLock)
		{
			// If player wants to move on X and Z axis only
			transform.Translate(targetPosByKey);
			newPosition.x = transform.position.x;
			newPosition.z = transform.position.z;
			transform.position = newPosition;
		}
		else
		{
			transform.Translate(targetPosByKey);
		}

		Rotate();

		StopCameraChange();
	}

	private void LockVerticalMovement()
	{
		_verticalMovementLock = !_verticalMovementLock;
		// Debug.Log(_verticalMovementLock);
		GetUIController()?.SetVerticalMovementLockToggle(_verticalMovementLock);
	}

	private UIController GetUIController()
	{
		if (_uiController != null)
		{
			return _uiController;
		}

		if (Main.Instance == null)
		{
			return null;
		}

		_uiController = Main.UIController;
		if (_uiController == null && Main.UIObject != null)
		{
			_uiController = Main.UIObject.GetComponent<UIController>();
		}

		return _uiController;
	}

	private void HandleMouseControl()
	{
		_lastMouse = (Vector3)Mouse.current.position.ReadValue() - _lastMouse;

		var mousePoseX = transform.eulerAngles.x - _lastMouse.y * _camSens;
		var mousePoseY = transform.eulerAngles.y + _lastMouse.x * _camSens;
		_lastMouse.Set(mousePoseX, mousePoseY, 0);

		// Mouse camera angle done.
		if (Mouse.current.leftButton.isPressed)
		{
			HandleLeftClickOnScreen();
		}
		else if (Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed)
		{
			HandleScreenOrbitControl();

			_terminateMoving = true;
		}
		else
		{
			_edgeSensAccumlated = 0.0f;
		}

		_lastMouse = (Vector3)Mouse.current.position.ReadValue();
	}

	private Vector3 HandleKeyboardCommands()
	{
		var p = GetBaseInput();
		if (Keyboard.current[Key.LeftShift].isPressed)
		{
			_totalRun += Time.deltaTime;
			p *= _totalRun * _shiftAdd;
			p.x = Mathf.Clamp(p.x, -_maxShift, _maxShift);
			p.y = Mathf.Clamp(p.y, -_maxShift, _maxShift);
			p.z = Mathf.Clamp(p.z, -_maxShift, _maxShift);
		}
		else
		{
			_totalRun = Mathf.Clamp(_totalRun * 0.5f, 1f, 1000f);
			p *= _mainSpeed;
		}
		return p;
	}

	private void HandleLeftClickOnScreen()
	{
		var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
		if (Physics.Raycast(ray.origin, ray.direction, out var hitInfo, 100f, _targetLayerMask))
		{
			if (!EventSystem.current.IsPointerOverGameObject())
			{
				var sdfPoint = Unity2SDF.Position(hitInfo.point);
				Main.InfoDisplay.SetPointInfo(sdfPoint);
			}
		}
	}

	private void HandleScreenOrbitControl()
	{
		// perspective move during the right or wheel click
		// Debug.Log(_lastMouse.ToString("F4"));
		if (_edgeSensAccumlated < _edgeSensMax)
		{
			_edgeSensAccumlated += _edgeSens;
		}

		var mousePos = Mouse.current.position.ReadValue();
		if (mousePos.x < _edgeWidth)
		{
			// Debug.Log("rotate camera left here");
			_lastMouse.y -= _edgeSensAccumlated;
		}
		else if (mousePos.x > Screen.width - _edgeWidth)
		{
			// Debug.Log("rotate camera right here");
			_lastMouse.y += _edgeSensAccumlated;
		}
		else if (mousePos.y < _edgeWidth)
		{
			// Debug.Log("rotate camera down here");
			_lastMouse.x += _edgeSensAccumlated;
		}
		else if (mousePos.y > Screen.height - _edgeWidth)
		{
			_lastMouse.x -= _edgeSensAccumlated;
		}
		else
		{
			_edgeSensAccumlated = 0.0f;
		}

		var xRotation =
			((_lastMouse.x >= -1 && _lastMouse.x < 90) ||
			 (_lastMouse.x > 270 && _lastMouse.x <= 361)) ?
				_lastMouse.x : transform.localRotation.eulerAngles.x;

		transform.localRotation = Quaternion.Euler(xRotation, _lastMouse.y, 0);
	}

	private void Rotate()
	{
		var rotation = transform.rotation.eulerAngles;
		if (!Keyboard.current[Key.LeftCtrl].isPressed)
		{
			if (Keyboard.current[Key.Q].isPressed)
			{
				transform.RotateAround(transform.position, Vector3.up, -_angleStep);
				_terminateMoving = true;
			}
			else if (Keyboard.current[Key.E].isPressed)
			{
				transform.RotateAround(transform.position, Vector3.up, _angleStep);
				_terminateMoving = true;
			}
		}
	}

	private CameraKeyOverlayInput GetActiveCameraKeyOverlayInputs()
	{
		var activeInputs = CameraKeyOverlayInput.None;

		if (Keyboard.current[Key.LeftCtrl].isPressed)
		{
			return activeInputs;
		}

		if (Keyboard.current[Key.W].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyW;
		}
		if (Keyboard.current[Key.A].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyA;
		}
		if (Keyboard.current[Key.S].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyS;
		}
		if (Keyboard.current[Key.D].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyD;
		}
		if (Keyboard.current[Key.Q].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyQ;
		}
		if (Keyboard.current[Key.E].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyE;
		}
		if (Keyboard.current[Key.R].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyR;
		}
		if (Keyboard.current[Key.F].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.KeyF;
		}
		if (activeInputs != CameraKeyOverlayInput.None && Keyboard.current[Key.LeftShift].isPressed)
		{
			activeInputs |= CameraKeyOverlayInput.LeftShift;
		}

		return activeInputs;
	}

	public void BlockControl()
	{
		_blockControl = true;
	}

	public void UnBlockControl()
	{
		_blockControl = false;
	}

	public void BlockMouseWheelControl(in bool value)
	{
		_blockMouseWheelControl = value;
	}

	protected abstract Vector3 HandleMouseWheelScroll();
	protected abstract Vector3 HandleKeyboardDirection();

	private Vector3 GetBaseInput()
	{
		//returns the basic values, if it's 0 than it's not active.
		var baseDirection = new Vector3();

		if (!_blockMouseWheelControl)
		{
			var scrollWheel = Mouse.current.scroll.ReadValue().y;
			if (scrollWheel != 0)
			{
				baseDirection += HandleMouseWheelScroll();
				// Debug.Log(scrollWheel.ToString("F4") + " | " + Input.mouseScrollDelta.y);
				_terminateMoving = true;
			}
		}

		if (!Keyboard.current[Key.LeftCtrl].isPressed)
		{
			baseDirection += HandleKeyboardDirection();
			if (Keyboard.current[Key.W].isPressed || Keyboard.current[Key.S].isPressed ||
				Keyboard.current[Key.A].isPressed || Keyboard.current[Key.D].isPressed ||
				Keyboard.current[Key.R].isPressed || Keyboard.current[Key.F].isPressed)
			{
				_terminateMoving = true;
			}
		}

		return baseDirection;
	}

	public void StartCameraChange(Pose targetPose)
	{
		if (_movingCoroutine != null)
		{
			StopCoroutine(_movingCoroutine);
		}

		_terminateMoving = false;
		_movingCoroutine = StartCoroutine(ChangeCameraView(targetPose));
	}

	public void StopCameraChange()
	{
		if (_terminateMoving && _movingCoroutine != null)
		{
			StopCoroutine(_movingCoroutine);
			_movingCoroutine = null;
			_terminateMoving = false;
		}
	}

	private IEnumerator ChangeCameraView(Pose targetPose)
	{
		var t = 0f;
		while (
			Vector3.Distance(transform.position, targetPose.position) > Vector3.kEpsilon ||
			Quaternion.Angle(transform.rotation, targetPose.rotation) > Quaternion.kEpsilon)
		{
			var smoothPosition = Vector3.Lerp(transform.position, targetPose.position, t);
			transform.position = smoothPosition;

			var smoothRotation = Quaternion.Lerp(transform.rotation, targetPose.rotation, t);
			transform.rotation = smoothRotation;

			t += MoveSmoothSpeed;

			yield return null;
		}
	}
}