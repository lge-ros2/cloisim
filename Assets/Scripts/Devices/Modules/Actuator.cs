/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class Actuator
{
	public enum MovingType {MoveTowards = 0, Lerp, SmoothDamp};
	private const float lerpEpsilon = 0.009f;
	private Vector3 currentVelocity = Vector3.zero; // only for SmoothDamp

	private Transform targetTransform = null;

	private MovingType movingType = MovingType.MoveTowards;

	private Vector3 targetPosition = Vector3.zero;

	private float distanceEpsilon = Vector3.kEpsilon;
	private float maxMovingSpeed = 1;
	private bool isMoving = false;
	public bool IsMoving => isMoving;

	public Vector3 TargetPosition => targetPosition;

	public void SetTarget(in string name)
	{
		var tempObject = GameObject.Find(name);
		if (tempObject != null)
		{
			SetTarget(tempObject);
		}
	}

	public void SetTarget(in GameObject target)
	{
		if (target != null)
		{
			var tempTranform = target.GetComponent<Transform>();
			SetTarget(tempTranform);
		}
	}

	public void SetTarget(in Transform target)
	{
		targetTransform = target;
	}

	public void SetMaxSpeed(in float value)
	{
		maxMovingSpeed = value;
	}

	public void SetMovingType(in MovingType type)
	{
		movingType = type;

		switch (movingType)
		{
			case MovingType.Lerp:
				distanceEpsilon = lerpEpsilon;
				break;

			case MovingType.SmoothDamp:
				distanceEpsilon = lerpEpsilon;
				break;

			case MovingType.MoveTowards:
			default:
				distanceEpsilon = Vector3.kEpsilon;
				break;
		}
	}

	public Vector3 CurrentPosition(in bool worldPosition = false)
	{
		if (targetTransform == null)
		{
			return Vector3.zero;
		}

		return (worldPosition)? targetTransform.position : targetTransform.localPosition;
	}

	//
	// Summary:
	//     Set target position before drive.
	//
	// Parameters:
	//   direction: Vector3.up/down/forwad/back/left/right
	//   offset: target offset
	//   relative: if true, target shall move from current position,
	//             otherwise target shall be set absolute offset along with direction
	//
	public void SetTargetPosition(in Vector3 direction, in float offset, in bool relative = false)
	{
		var targetValue = (direction * offset);

		if (relative)
		{
			targetPosition = CurrentPosition() + targetValue;
		}
		else
		{
			targetPosition = CurrentPosition();
			SetZeroOnDirection(ref targetPosition, direction);
			targetPosition += targetValue;
		}

		// Debug.Log("SetTargetPosition: " + targetPosition.ToString("F5"));
	}

	public void SetTargetPosition(in Vector3 absolutePosition)
	{
		targetPosition = absolutePosition;
	}

	public void Drive()
	{
		if (targetTransform == null)
		{
			Debug.LogError("target Transform is null !!!!!!!!!!!");
			isMoving = false;
			return;
		}

		isMoving = true;

		var nextPosition = Vector3.zero;
		switch (movingType)
		{
			case MovingType.Lerp:
				nextPosition = Vector3.Lerp(CurrentPosition(), targetPosition, maxMovingSpeed * Time.deltaTime);
				break;

			case MovingType.SmoothDamp:
				nextPosition = Vector3.SmoothDamp(CurrentPosition(), targetPosition, ref currentVelocity, Time.deltaTime, maxMovingSpeed);
				break;

			case MovingType.MoveTowards:
			default:
				nextPosition = Vector3.MoveTowards(CurrentPosition(), targetPosition, maxMovingSpeed * Time.deltaTime);
				break;
		}

		targetTransform.localPosition = nextPosition;

		// check if it arrived
		var distance = Vector3.SqrMagnitude(targetPosition - nextPosition);
 		if (distance < distanceEpsilon)
		{
			// final touch
			targetTransform.localPosition = targetPosition;

			// stop
			isMoving = false;
		}
	}

	public bool IsSamePosition(in Vector3 checkPosition)
	{
		return DeviceHelper.IsSamePosition(CurrentPosition(), checkPosition);
	}

	public bool IsSamePosition(in Vector3 checkDirection, in float checkOffset)
	{
		var tempTarget = CurrentPosition();
		SetZeroOnDirection(ref tempTarget, checkDirection);
		tempTarget += (checkDirection * checkOffset);
		return DeviceHelper.IsSamePosition(CurrentPosition(), tempTarget);
	}

	public static void SetZeroOnDirection(ref Vector3 target, in Vector3 direction)
	{
		if (direction.Equals(Vector3.left) || direction.Equals(Vector3.right))
		{
			target.x = 0;
		}
		else if (direction.Equals(Vector3.up) || direction.Equals(Vector3.down))
		{
			target.y = 0;
		}
		else if (direction.Equals(Vector3.forward) || direction.Equals(Vector3.back))
		{
			target.z = 0;
		}
	}
}