/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class DoorsControl : MonoBehaviour
{
	private Actuator doorLeft = null;
	private Actuator doorRight = null;

	private Vector3 closedDoorPositionLeft = Vector3.zero;
	private Vector3 closedDoorPositionRight = Vector3.zero;
	private Vector3 openedDoorPositionLeft = Vector3.zero;
	private Vector3 openedDoorPositionRight = Vector3.zero;

	private Vector3 doorTargetPositionLeft = Vector3.zero;
	private Vector3 doorTargetPositionRight = Vector3.zero;

	public float speed = 0.1f;
	public float openOffset = 1;

	public DoorsControl()
	{
		doorLeft = new Actuator();
		doorRight = new Actuator();
	}

	void Start()
	{
		doorLeft.SetMovingType(Actuator.MovingType.MoveTowards);
		doorLeft.SetMaxSpeed(speed);
		doorRight.SetMovingType(Actuator.MovingType.MoveTowards);
		doorRight.SetMaxSpeed(speed);
	}

	public void SetLeftDoor(in Transform targetTransform)
	{
		doorLeft.SetTarget(targetTransform);

		if (targetTransform == null)
		{
			closedDoorPositionLeft = Vector3.zero;
			openedDoorPositionLeft = Vector3.zero;
		}
		else
		{
			closedDoorPositionLeft = doorLeft.CurrentPosition();
			openedDoorPositionLeft = Vector3.right * openOffset;
		}
	}

	public void SetRightDoor(in Transform targetTransform)
	{
		doorRight.SetTarget(targetTransform);

		if (targetTransform == null)
		{
			closedDoorPositionRight = Vector3.zero;
			openedDoorPositionRight = Vector3.zero;
		}
		else
		{
			closedDoorPositionRight = doorRight.CurrentPosition();
			openedDoorPositionRight = Vector3.left * openOffset;
		}
	}

	public Vector3 GetLeftDoorPosition()
	{
		return doorLeft.CurrentPosition(true);
	}

	public Vector3 GetRightDoorPosition()
	{
		return doorRight.CurrentPosition(true);
	}

	public void Open()
	{
		doorLeft.SetTargetPosition(Vector3.right, openOffset);
		doorRight.SetTargetPosition(Vector3.left, openOffset);

		if (IsClosed() && !IsMoving())
		{
			StartCoroutine(MoveTo());
		}
	}

	public void Close()
	{
		doorLeft.SetTargetPosition(Vector3.left, openOffset, true);
		doorRight.SetTargetPosition(Vector3.right, openOffset, true);

		if (IsOpened() && !IsMoving())
		{
			StartCoroutine(MoveTo());
		}
	}


	public bool IsOpened()
	{
		if (doorLeft.IsSamePosition(openedDoorPositionLeft) && doorRight.IsSamePosition(openedDoorPositionRight))
		{
			return true;
		}

		return false;
	}

	public bool IsClosed()
	{
		if (doorLeft.IsSamePosition(closedDoorPositionLeft) && doorRight.IsSamePosition(closedDoorPositionRight))
		{
			return true;
		}

		return false;
	}

	public bool IsMoving()
	{
		if (doorLeft.IsMoving || doorRight.IsMoving)
		{
			return true;
		}

		return false;
	}

	private IEnumerator MoveTo()
	{
		var waitForFixedUpdate = new WaitForFixedUpdate();

		// Debug.Log(name + "::" + doorTargetPositionLeft.ToString("F5"));
		// Debug.Log(name + "::" + doorTargetPositionRight.ToString("F5"));

		do
		{
			doorLeft.Drive();
			doorRight.Drive();

			yield return waitForFixedUpdate;

		} while (doorLeft.IsMoving || doorRight.IsMoving);
	}

// #if UNITY_EDITOR
// 	// just for test
// 	void Update()
// 	{
// 		if (Input.GetKeyUp(KeyCode.I))
// 		{
// 			Close();
// 		}
// 		else if (Input.GetKeyUp(KeyCode.O))
// 		{
// 			Open();
// 		}
// 	}
// #endif
}