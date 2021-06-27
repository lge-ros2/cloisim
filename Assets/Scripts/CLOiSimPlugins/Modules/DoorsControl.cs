/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class DoorsControl : MonoBehaviour
{
	private Actuator doorLeft = new Actuator();
	private Actuator doorRight = new Actuator();

	private Coroutine operatingDoor = null;

	private Vector3 doorTargetPositionLeft = Vector3.zero;
	private Vector3 doorTargetPositionRight = Vector3.zero;

	public float speed = 0.1f;
	public float openOffset = 1;

	void Start()
	{
		doorLeft.SetMovingType(Actuator.MovingType.MoveTowards);
		doorLeft.SetMaxSpeed(speed);
		doorLeft.SetDirection(Vector3.forward);
		doorLeft.SetMaxOffset(openOffset);
		doorLeft.SetMinOffset(0);
		doorRight.SetMovingType(Actuator.MovingType.MoveTowards);
		doorRight.SetMaxSpeed(speed);
		doorRight.SetDirection(Vector3.back);
		doorRight.SetMaxOffset(openOffset);
		doorRight.SetMinOffset(0);
	}

	public void SetLeftDoor(in Transform targetTransform)
	{
		doorLeft.SetTarget(targetTransform);
	}

	public void SetRightDoor(in Transform targetTransform)
	{
		doorRight.SetTarget(targetTransform);
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
		doorLeft.SetTargetPosition(openOffset);
		doorRight.SetTargetPosition(openOffset);

		if (operatingDoor != null)
		{
			StopCoroutine(operatingDoor);
		}

		operatingDoor = StartCoroutine(Operate());
	}

	public void Close()
	{
		doorLeft.SetTargetPosition(-openOffset);
		doorRight.SetTargetPosition(-openOffset);

		if (operatingDoor != null)
		{
			StopCoroutine(operatingDoor);
		}
		operatingDoor = StartCoroutine(Operate());
	}

	public bool IsOpened()
	{
		if (doorLeft.IsReachedMax() && doorRight.IsReachedMax())
		{
			return true;
		}

		return false;
	}

	public bool IsClosed()
	{
		if (doorLeft.IsReachedMin() && doorRight.IsReachedMin())
		{
			return true;
		}

		return false;
	}

	private IEnumerator Operate()
	{
		// Debug.Log(name + "::" + doorTargetPositionLeft.ToString("F5"));
		// Debug.Log(name + "::" + doorTargetPositionRight.ToString("F5"));
		var waitForEOF = new WaitForEndOfFrame();

		do
		{
			doorLeft.Drive();
			doorRight.Drive();
			yield return waitForEOF;

		} while (doorLeft.IsMoving || doorRight.IsMoving);

		yield return null;
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