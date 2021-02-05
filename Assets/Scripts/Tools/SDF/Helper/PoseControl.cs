/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class PoseControl
		{
			private UE.Transform _targetTransform = null;
			private List<UE.Pose> _poseList = new List<UE.Pose>();
			private bool isWorldPosition = false;

			public PoseControl(in UE.Transform target)
			{
				_targetTransform = target;
				// SetTransform(target);
			}

			public void SetWorldPosition(in bool enable)
			{
				isWorldPosition = enable;
			}

			// public void SetTransform(in UE.Transform target)
			// {
			// 	if (target != null)
			// 	{
			// 		_targetTransform = target;
			// 	}
			// }

			public void Add(in UE.Vector3 newPosition, in UE.Quaternion newRotation)
			{
				_poseList.Add(new UE.Pose(newPosition, newRotation));
			}

			public UE.Pose Get(in int targetFrame = 0)
			{
				return _poseList[targetFrame];
			}

			public void Reset(in int targetFrame = 0)
			{
				// Debug.LogFormat("Reset Transform isWorldPos({0})!!", isWorldPosition);
				if (_poseList.Count == 0)
				{
					UE.Debug.LogWarning("Nothing to reset, pose List is empty");
					return;
				}

				if (targetFrame >= _poseList.Count)
				{
					UE.Debug.LogWarningFormat("exceed target frame({0}) in _poseList({1})", targetFrame, _poseList.Count);
					return;
				}

				if (_targetTransform != null)
				{
					var targetPose = Get(targetFrame);

					if (isWorldPosition)
					{
						_targetTransform.position = targetPose.position;
						_targetTransform.rotation = targetPose.rotation;
					}
					else
					{
						_targetTransform.localPosition = targetPose.position;;
						_targetTransform.localRotation = targetPose.rotation;;
					}
				}
			}
		}
	}
}