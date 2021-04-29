/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

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

	public bool blockControl = false;

	private bool _verticalMovementLock = false;

	public bool VerticalMovementLock
	{
		set => _verticalMovementLock = value;
		get => _verticalMovementLock;
	}

	public float mainSpeed = 10.0f; //regular speed
	public float shiftAdd = 20.0f; //multiplied by how long shift is held.  Basically running
	public float maxShift = 50.0f; //Maximum speed when holding shift
	public float camSens = 0.1f; //How sensitive it with mouse
	public float edgeWidth = 100.0f;
	public float edgeSens = 0.02f;
	public float edgeSensMax = 1.0f;

	private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
	private float totalRun = 1.0f;
	private float edgeSensAccumlated = 0.0f;

	void LateUpdate()
	{
		if (blockControl)
		{
			return;
		}

		if (Input.GetKeyUp(KeyCode.Space))
		{
			_verticalMovementLock = !_verticalMovementLock;
			// Debug.Log(_verticalMovementLock);
		}

		lastMouse = Input.mousePosition - lastMouse;
		lastMouse.Set(-lastMouse.y * camSens, lastMouse.x * camSens, 0);
		lastMouse.Set(transform.eulerAngles.x + lastMouse.x, transform.eulerAngles.y + lastMouse.y , 0);

		//Mouse camera angle done.
		if (Input.GetMouseButton(2) || Input.GetMouseButton(1))
		{
			// perspective move during the right or wheel click
			// Debug.Log(lastMouse.ToString("F4"));

			if (edgeSensAccumlated < edgeSensMax)
			{
				edgeSensAccumlated += edgeSens;
			}

			if (Input.mousePosition.x < edgeWidth)
			{
				// Debug.Log("rotate camera left here");
				lastMouse.y -= edgeSensAccumlated;
				transform.eulerAngles = lastMouse;
			}
			else if (Input.mousePosition.x > Screen.width - edgeWidth)
			{
				// Debug.Log("rotate camera right here");
				lastMouse.y += edgeSensAccumlated;
				transform.eulerAngles = lastMouse;
			}
			else  if (Input.mousePosition.y < edgeWidth)
			{
				// Debug.Log("rotate camera down here");
				lastMouse.x += edgeSensAccumlated;
				transform.eulerAngles = lastMouse;
			}
			else if (Input.mousePosition.y > Screen.height - edgeWidth)
			{
				// Debug.Log("rotate camera up here");
				lastMouse.x -= edgeSensAccumlated;
				transform.eulerAngles = lastMouse;
			}
			else
			{
				edgeSensAccumlated = 0.0f;
				transform.eulerAngles = lastMouse;
			}
		}
		else
		{
			edgeSensAccumlated = 0.0f;
		}

		lastMouse = Input.mousePosition;

		// Keyboard commands
		var p = GetBaseInput();
		if (Input.GetKey(KeyCode.LeftShift))
		{
			totalRun += Time.deltaTime;
			p *= (totalRun * shiftAdd);
			p.x = Mathf.Clamp(p.x, -maxShift, maxShift);
			p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
			p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
		}
		else
		{
			totalRun = Mathf.Clamp(totalRun * 0.5f, 1f, 1000f);
			p *= mainSpeed;
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
	}

	private Vector3 GetBaseInput()
	{
		//returns the basic values, if it's 0 than it's not active.
		var baseDirection = new Vector3();

		if (Input.GetKey(KeyCode.W) || Input.GetAxis("Mouse ScrollWheel") > 0)
		{
			baseDirection += new Vector3(0, 0, 1);
		}
		else if (Input.GetKey(KeyCode.S) || Input.GetAxis("Mouse ScrollWheel") < 0)
		{
			baseDirection += new Vector3(0, 0, -1);
		}

		if (Input.GetKey(KeyCode.A))
		{
			baseDirection += new Vector3(-1, 0, 0);
		}
		else if (Input.GetKey(KeyCode.D))
		{
			baseDirection += new Vector3(1, 0, 0);
		}

		if (Input.GetKey(KeyCode.Q))
		{
			baseDirection += new Vector3(0, 1, 0);
		}
		else if (Input.GetKey(KeyCode.Z))
		{
			baseDirection += new Vector3(0, -1, 0);
		}

		return baseDirection;
	}
}