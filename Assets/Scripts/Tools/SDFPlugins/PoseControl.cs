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
	private List<Pose> poseList = new List<Pose>();
	private bool isWorldPosition = false;

	public PoseControl(in Transform target = null)
	{

		SetTransform(target);
	}

	public void SetWorldPosition(in bool enable)
	{
		isWorldPosition = enable;
	}

	public void SetTransform(in Transform target)
	{
		if (target != null)
		{
			targetTransform = target;
		}
	}

	public void Add(in Vector3 newPosition, in Quaternion newRotation)
	{
		poseList.Add(new Pose(newPosition, newRotation));
	}

	public Pose Get(in int targetFrame = 0)
	{
		return poseList[targetFrame];
	}

	public void Reset(in int targetFrame = 0)
	{
		// Debug.LogFormat("Reset Transform isWorldPos({0})!!", isWorldPosition);
		if (poseList.Count == 0)
		{
			Debug.LogWarning("Nothing to reset, pose List is empty");
			return;
		}

		if (targetFrame >= poseList.Count)
		{
			Debug.LogWarningFormat("exceed target frame({0}) in poseList({1})", targetFrame, poseList.Count);
			return;
		}

		if (targetTransform != null)
		{
			var targetPose = Get(targetFrame);

			if (isWorldPosition)
			{
				targetTransform.position = targetPose.position;
				targetTransform.rotation = targetPose.rotation;
			}
			else
			{
				targetTransform.localPosition = targetPose.position;;
				targetTransform.localRotation = targetPose.rotation;;
			}
		}
	}
}