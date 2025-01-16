/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(90)]
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

	[SerializeField]
	protected const float MoveSmoothSpeed = .0025f;

	[SerializeField]
	protected float _mainSpeed = 10.0f; // regular speed

	[SerializeField]
	protected float _shiftAdd = 20.0f; // multiplied by how long shift is held.  Basically running

	[SerializeField]
	protected float _maxShift = 50.0f; // Maximum speed when holding shift

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
		_uiController = Main.UIObject?.GetComponent<UIController>();
		// Debug.Log(_uiController);
	}

	void LateUpdate()
	{
		if (_blockControl)
		{
			return;
		}

		if (Input.GetKeyUp(KeyCode.Space))
		{
			LockVerticalMovement();
		}

		_lastMouse = Input.mousePosition - _lastMouse;
		_lastMouse.Set(-_lastMouse.y * _camSens, _lastMouse.x * _camSens, 0);
		_lastMouse.Set(transform.eulerAngles.x + _lastMouse.x, transform.eulerAngles.y + _lastMouse.y, 0);

		// Mouse camera angle done.
		if (Input.GetMouseButton(0))
		{
			HandleLeftClickOnScreen();
		}
		else if (Input.GetMouseButton(2) || Input.GetMouseButton(1))
		{
			HandleScreenOrbitControl();

			_terminateMoving = true;
		}
		else
		{
			_edgeSensAccumlated = 0.0f;
		}

		_lastMouse = Input.mousePosition;

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
		_uiController.SetVerticalMovementLockToggle(_verticalMovementLock);
	}

	private Vector3 HandleKeyboardCommands()
	{
		var p = GetBaseInput();
		if (Input.GetKey(KeyCode.LeftShift))
		{
			_totalRun += Time.deltaTime;
			p *= (_totalRun * _shiftAdd);
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
		var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

		if (Input.mousePosition.x < _edgeWidth)
		{
			// Debug.Log("rotate camera left here");
			_lastMouse.y -= _edgeSensAccumlated;
			transform.eulerAngles = _lastMouse;
		}
		else if (Input.mousePosition.x > Screen.width - _edgeWidth)
		{
			// Debug.Log("rotate camera right here");
			_lastMouse.y += _edgeSensAccumlated;
			transform.eulerAngles = _lastMouse;
		}
		else if (Input.mousePosition.y < _edgeWidth)
		{
			// Debug.Log("rotate camera down here");
			_lastMouse.x += _edgeSensAccumlated;
			transform.eulerAngles = _lastMouse;
		}
		else if (Input.mousePosition.y > Screen.height - _edgeWidth)
		{
			// Debug.Log("rotate camera up here");
			_lastMouse.x -= _edgeSensAccumlated;
			transform.eulerAngles = _lastMouse;
		}
		else
		{
			_edgeSensAccumlated = 0.0f;
			transform.eulerAngles = _lastMouse;
		}
	}

	private void Rotate()
	{
		var rotation = transform.rotation.eulerAngles;
		if (!Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKey(KeyCode.Q))
			{
				transform.RotateAround(transform.position, Vector3.up, -_angleStep);
				_terminateMoving = true;
			}
			else if (Input.GetKey(KeyCode.E))
			{
				transform.RotateAround(transform.position, Vector3.up, _angleStep);
				_terminateMoving = true;
			}
		}
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
			var scrollWheel = Input.GetAxisRaw("Mouse ScrollWheel");
			if (scrollWheel != 0)
			{
				baseDirection += HandleMouseWheelScroll();
				// Debug.Log(scrollWheel.ToString("F4") + " | " + Input.mouseScrollDelta.y);
				_terminateMoving = true;
			}
		}

		if (!Input.GetKey(KeyCode.LeftControl))
		{
			baseDirection += HandleKeyboardDirection();

			if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
				Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
				Input.GetKey(KeyCode.G) || Input.GetKey(KeyCode.F))
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
		_movingCoroutine = StartCoroutine(ChangeCameraView(targetPose));
	}

	public void StopCameraChange()
	{
		if (_terminateMoving && _movingCoroutine != null)
		{
			StopCoroutine(_movingCoroutine);
			_terminateMoving = false;
		}
	}

	private IEnumerator ChangeCameraView(Pose targetPose)
	{
		var t = 0f;
		while (
			Vector3.Distance(transform.position, targetPose.position) > Vector3.kEpsilon &&
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