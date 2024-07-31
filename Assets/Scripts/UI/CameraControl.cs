/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DefaultExecutionOrder(90)]
public class CameraControl : MonoBehaviour
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

	private bool _blockControl = false;

	private bool _blockMouseWheelControl = false;

	private bool _verticalMovementLock = false;

	private bool _terminateMoving = false;

	private UIController _uiController = null;

	public bool VerticalMovementLock
	{
		set => _verticalMovementLock = value;
		get => _verticalMovementLock;
	}

	private const float MoveSmoothSpeed = .05f;

	[SerializeField]
	private float _mainSpeed = 10.0f; // regular speed

	[SerializeField]
	private float _shiftAdd = 20.0f; // multiplied by how long shift is held.  Basically running

	[SerializeField]
	private float _maxShift = 50.0f; // Maximum speed when holding shift

	[SerializeField]
	private float _camSens = 0.1f; // How sensitive it with mouse

	[SerializeField]
	private float _edgeWidth = 100.0f;

	[SerializeField]
	private float _edgeSens = 0.02f;

	[SerializeField]
	private float _edgeSensMax = 1.0f;

	[SerializeField]
	private float _wheelMoveAmp = 50f;

	[SerializeField]
	private float _angleStep = 1.5f;

	private Vector3 _lastMouse = new Vector3(255, 255, 255); // kind of in the middle of the screen, rather than at the top (play)
	private float _totalRun = 1.0f;
	private float _edgeSensAccumlated = 0.0f;

	private int _targetLayerMask = 0;

	Coroutine _movingCoroutine = null;

	void Awake()
	{
		_targetLayerMask = LayerMask.GetMask("Default");
		_uiController = Main.UIObject?.GetComponent<UIController>();
		// Debug.Log(_uiController);;
	}

	void LateUpdate()
	{
		if (_blockControl)
		{
			return;
		}

		if (Input.GetKeyUp(KeyCode.Space))
		{
			_verticalMovementLock = !_verticalMovementLock;
			// Debug.Log(_verticalMovementLock);
			_uiController.SetVerticalMovementLockToggle(_verticalMovementLock);
		}

		_lastMouse = Input.mousePosition - _lastMouse;
		_lastMouse.Set(-_lastMouse.y * _camSens, _lastMouse.x * _camSens, 0);
		_lastMouse.Set(transform.eulerAngles.x + _lastMouse.x, transform.eulerAngles.y + _lastMouse.y, 0);

		// Mouse camera angle done.
		if (Input.GetMouseButton(0))
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
		else if (Input.GetMouseButton(2) || Input.GetMouseButton(1))
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

			_terminateMoving = true;
		}
		else
		{
			_edgeSensAccumlated = 0.0f;
		}

		_lastMouse = Input.mousePosition;

		// Keyboard commands for Translation
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

		p *= Time.deltaTime;

		var newPosition = transform.position;
		if (_verticalMovementLock)
		{
			// If player wants to move on X and Z axis only
			transform.Translate(p);
			newPosition.x = transform.position.x;
			newPosition.z = transform.position.z;
			transform.position = newPosition;
		}
		else
		{
			transform.Translate(p);
		}

		Rotate();

		if (_terminateMoving && _movingCoroutine != null)
		{
			StopCoroutine(_movingCoroutine);
			_terminateMoving = false;
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

	private Vector3 GetBaseInput()
	{
		//returns the basic values, if it's 0 than it's not active.
		var baseDirection = new Vector3();

		if (!_blockMouseWheelControl)
		{
			var scrollWheel = Input.GetAxisRaw("Mouse ScrollWheel");
			if (scrollWheel != 0)
			{
				baseDirection += new Vector3(0, 0, Input.mouseScrollDelta.y * _wheelMoveAmp);
				// Debug.Log(scrollWheel.ToString("F4") + " | " + Input.mouseScrollDelta.y);
				_terminateMoving = true;
			}
		}

		if (!Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKey(KeyCode.W))
			{
				baseDirection.z += 1;
			}
			else if (Input.GetKey(KeyCode.S))
			{
				baseDirection.z += -1;
			}

			if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
			{
				baseDirection.x = 0;
			}
			else if (Input.GetKey(KeyCode.A))
			{
				baseDirection.x += -1;
			}
			else if (Input.GetKey(KeyCode.D))
			{
				baseDirection.x += 1;
			}

			if (Input.GetKey(KeyCode.R))
			{
				baseDirection.y += 1;
			}
			else if (Input.GetKey(KeyCode.F))
			{
				baseDirection.y += -1;
			}

			if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
				Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
				Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.F))
			{
				_terminateMoving = true;
			}
		}

		return baseDirection;
	}

	public void Move(Pose targetPose)
	{
		if (_movingCoroutine != null)
		{
			StopCoroutine(_movingCoroutine);
		}
		_movingCoroutine = StartCoroutine(ChangeCameraView(targetPose));
	}

	private IEnumerator ChangeCameraView(Pose targetPose)
	{
		while (
			Vector3.Distance(transform.position, targetPose.position) > Vector3.kEpsilon &&
			Quaternion.Angle(transform.rotation, targetPose.rotation) > Quaternion.kEpsilon)
		{
			var smoothPosition = Vector3.Lerp(transform.position, targetPose.position, MoveSmoothSpeed);
			transform.position = smoothPosition;

			var smoothRotation = Quaternion.Lerp(transform.rotation, targetPose.rotation, MoveSmoothSpeed);
			transform.rotation = smoothRotation;

			yield return null;
		}
	}
}