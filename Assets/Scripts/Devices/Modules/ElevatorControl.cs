/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class ElevatorControl : MonoBehaviour
{
	public LiftControl liftControl;
	public DoorsControl doorsControl;
	public DoorsControl outsideDoorsControl;

	public const float outsideDoorDistance = 0.10f;
	public string doorLinkNameLeft = string.Empty;
	public string doorLinkNameRight = string.Empty;

	public string outsideDoorLinkNameLeft = string.Empty;
	public string outsideDoorLinkNameRight = string.Empty;

	public float doorAutoClosingTimer = 0;
	private float currentElevatorHeight = 0;
	private Coroutine runningDoorAutoClosing = null;

	public float Height
	{
		get => currentElevatorHeight;
		set
		{
			var elevatorPosition = transform.position;
			elevatorPosition.y = value;
			transform.position = elevatorPosition;
		}
	}

	void Awake()
	{
		liftControl = gameObject.AddComponent<LiftControl>();
		doorsControl = gameObject.AddComponent<DoorsControl>();
		outsideDoorsControl = gameObject.AddComponent<DoorsControl>();
	}

	void Start()
	{
		liftControl.SetFinishedEventListener(FindAndSetOutsideDoor);

		// find elevator door inside
		foreach (var link in GetComponentsInChildren<LinkPlugin>())
		{
			if (link.name.Equals(doorLinkNameLeft))
			{
				doorsControl.SetLeftDoor(link.transform);
			}
			else if (link.name.Equals(doorLinkNameRight))
			{
				doorsControl.SetRightDoor(link.transform);
			}
		}
	}

	void Update()
	{
		currentElevatorHeight = transform.position.y;
	}

	public bool IsDoorOpened()
	{
		return doorsControl.IsOpened();
	}

	public void OpenDoor()
	{
		// find outside door
		FindAndSetOutsideDoor();

		doorsControl.Open();

		// start Auto Closing doodr
		StopAutoClosingDoor();
		runningDoorAutoClosing = StartCoroutine(AutoClosingDoor());

		// control outside door
		outsideDoorsControl.Open();
	}

	public void CloseDoor()
	{
		doorsControl.Close();

		// control outside door
		outsideDoorsControl.Close();

		// stop Auto Closing
		StopAutoClosingDoor();
	}

	private void StopAutoClosingDoor()
	{
		if (runningDoorAutoClosing != null)
		{
			StopCoroutine(runningDoorAutoClosing);
			runningDoorAutoClosing = null;
		}
	}

	private IEnumerator AutoClosingDoor()
	{
		var waitForFixedUpdate = new WaitForFixedUpdate();

		var doorAutoClosingTimeElapsed = 0f;
		while (doorAutoClosingTimeElapsed < doorAutoClosingTimer)
		{
			doorAutoClosingTimeElapsed += Time.deltaTime;
			yield return waitForFixedUpdate;
		}

		// Debug.LogWarning("Close door automatically");

		CloseDoor();
	}

	public void FindAndSetOutsideDoor()
	{
		var elevatorSystemObject = transform.parent.gameObject;
		foreach (var link in elevatorSystemObject.GetComponentsInChildren<LinkPlugin>())
		{
			// find only nearset object
			var leftDoorDistance = Vector3.Distance(link.transform.position, doorsControl.GetLeftDoorPosition());
			var rightDoorDistance = Vector3.Distance(link.transform.position, doorsControl.GetRightDoorPosition());

			if ((leftDoorDistance < outsideDoorDistance) && link.name.Equals(outsideDoorLinkNameLeft))
			{
				outsideDoorsControl.SetLeftDoor(link.transform);
			}
			else if ((rightDoorDistance < outsideDoorDistance) && link.name.Equals(outsideDoorLinkNameRight))
			{
				outsideDoorsControl.SetRightDoor(link.transform);
			}
		}
	}

	public void UnsetOutsideDoor()
	{
		outsideDoorsControl.SetLeftDoor(null);
		outsideDoorsControl.SetRightDoor(null);
	}

	public bool IsDoorClosed()
	{
		return (doorsControl.IsClosed() && outsideDoorsControl.IsClosed()) ? true : false;
	}

	public void MoveTo(in float height)
	{
		UnsetOutsideDoor();

		liftControl.MoveTo(height);
	}

	public bool IsMoving()
	{
		return liftControl.IsMoving;
	}

	public bool IsArrived(in float height)
	{
		if (liftControl.IsMoving)
		{
			return false;
		}

		return DeviceHelper.IsSamePosition(currentElevatorHeight, height);
	}

// #if UNITY_EDITOR
// 	// just for test
// 	void Update()
// 	{
// 		if (IsDoorOpened() && Input.GetKeyUp(KeyCode.K))
// 		{
// 			CloseDoor();
// 		}
// 		else if (IsDoorClosed() && Input.GetKeyUp(KeyCode.L))
// 		{
// 			OpenDoor();
// 		}

// 		if (!liftControl.IsMoving && IsDoorClosed())
// 		{
// 			if (Input.GetKeyUp(KeyCode.U))
// 			{
// 				MoveTo(600);
// 			}
// 			else if (Input.GetKeyUp(KeyCode.J))
// 			{
// 				MoveTo(-600);
// 			}
// 		}
// 	}
// #endif
}