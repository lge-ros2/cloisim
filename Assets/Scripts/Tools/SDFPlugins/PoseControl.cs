/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class PoseControl
{
	private Transform targetTransform = null;
	private List<Vector3> positionList = null;
	private List<Quaternion> rotationList = null;
	private bool isWorldPosition = false;

	public PoseControl()
	{
		positionList = new List<Vector3>();
		rotationList = new List<Quaternion>();
	}

	public PoseControl(in Transform target)
		: this()
	{
		SetTransform(target);
	}

	public void SetWorldPosition(in bool enable)
	{
		isWorldPosition = enable;
	}

	public void SetTransform(in Transform target)
	{
		targetTransform = target;
	}

	public void Add(in Vector3 newPosition, in Quaternion newRotation)
	{
		AddPosition(newPosition);
		AddRotation(newRotation);
	}

	public void AddPosition(in Vector3 newPosition)
	{
		positionList.Add(newPosition);
	}

	public void AddRotation(in Quaternion newRotation)
	{
		rotationList.Add(newRotation);
	}

	public void Reset(in int targetFrame = 0)
	{
		// Debug.LogFormat("Reset Transform isWorldPos({0})!!", isWorldPosition);
		if (positionList.Count == 0)
		{
			Debug.LogWarning("Nothing to reset, position List is empty");
			return;
		}

		if (rotationList.Count == 0)
		{
			Debug.LogWarning("Nothing to reset, rotation List is empty");
			return;
		}

		if (targetFrame >= positionList.Count)
		{
			Debug.LogWarningFormat("exceed target frame({0}) in positionList({1})", targetFrame, positionList.Count);
			return;
		}

		if (targetFrame >= rotationList.Count)
		{
			Debug.LogWarningFormat("exceed target frame({0}) in rotationList({1})", targetFrame, rotationList.Count);
			return;
		}

		var targetPosition = positionList[targetFrame];
		var targetRotation = rotationList[targetFrame];

		if (targetTransform != null)
		{
			if (isWorldPosition)
			{
				targetTransform.position = targetPosition;
				targetTransform.rotation = targetRotation;
			}
			else
			{
				targetTransform.localPosition = targetPosition;
				targetTransform.localRotation = targetRotation;
			}
		}
	}
}