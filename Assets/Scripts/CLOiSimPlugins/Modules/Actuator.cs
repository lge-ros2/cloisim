/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Actuator
{
	public enum MovingType {MoveTowards = 0, Lerp, SmoothDamp};
	private const float lerpEpsilon = 0.009f;
	private Vector3 currentVelocity = Vector3.zero; // only for SmoothDamp

	private Vector3 initialPose = Vector3.zero;
	private Transform targetTransform = null;

	private MovingType movingType = MovingType.MoveTowards;

	private Vector3 targetPosition = Vector3.zero;

	#region Parameters
	private Vector3 rodDirection = Vector3.zero;
	private float minOffset = 0f;
	private float maxOffset = 0f;
	private Vector3 minPosition = Vector3.zero;
	private Vector3 maxPosition = Vector3.zero;
	#endregion

	private float distanceEpsilon = Vector3.kEpsilon;
	private float maxMovingSpeed = 1;
	private bool isMoving = false;
	public bool IsMoving => isMoving;

	public void SetDirection(in Vector3 direction)
	{
		rodDirection = direction;
		RecalculateInitialPoseByRodDirection();
	}

	public void SetMinOffset(in float offset)
	{
		if (minOffset > maxOffset)
		{
			Debug.LogWarningFormat("minOffset({0}) cannot be lower than maxOffset({1})", minOffset, maxOffset);
		}
		else
		{
			minOffset = offset;
			minPosition = rodDirection * minOffset;
		}
	}

	public void SetMaxOffset(in float offset)
	{
		if (maxOffset < minOffset)
		{
			Debug.LogWarningFormat("maxOffset({0}) cannot be lower than minOffset({1})", maxOffset, minOffset);
		}
		else
		{
			maxOffset = offset;
			maxPosition = rodDirection * maxOffset;
		}
	}

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

	public void SetInitialPose(in Vector3 targetPosition)
	{
		initialPose = targetPosition;
		RecalculateInitialPoseByRodDirection();
	}

	private void RecalculateInitialPoseByRodDirection()
	{
		initialPose.x -= (initialPose.x * rodDirection.x);
		initialPose.y -= (initialPose.y * rodDirection.y);
		initialPose.z -= (initialPose.z * rodDirection.z);
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
	//   offset: target offset
	//
	public void SetTargetPosition(float offset)
	{
		if (offset < minOffset)
		{
			offset = minOffset;
		}
		else if (offset > maxOffset)
		{
			offset = maxOffset;
		}

		targetPosition = initialPose + (rodDirection * offset);
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

		if (float.IsNaN(nextPosition.x) || float.IsNaN(nextPosition.y) || float.IsNaN(nextPosition.z))
		{
			Debug.Log("next position is NaN, Stop moving");

			// stop
			isMoving = false;
		}
		else
		{
			targetTransform.localPosition = nextPosition;

			// check if it arrived
			var distance = Vector3.Distance(targetPosition, nextPosition);
			if (distance < distanceEpsilon)
			{
				// final touch
				targetTransform.localPosition = targetPosition;

				// stop
				isMoving = false;
			}
		}
	}

	public bool IsReachedMin()
	{
		return IsSamePosition(minPosition);
	}

	public bool IsReachedMax()
	{
		return IsSamePosition(maxPosition);
	}

	public bool IsSamePosition(in Vector3 checkPosition)
	{
		return DeviceHelper.IsSamePosition(CurrentPosition(), checkPosition);
	}
}