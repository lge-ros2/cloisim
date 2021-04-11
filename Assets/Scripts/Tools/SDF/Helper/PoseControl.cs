/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Helper
	{
		public class PoseControl
		{
			private UE.Transform _targetTransform = null;
			private UE.ArticulationBody _articulationBody = null;

			private List<UE.Pose> _poseList = new List<UE.Pose>();

			public int Count => _poseList.Count;

			public PoseControl(in UE.Transform target)
			{
				_targetTransform = target;
			}

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
				if (_poseList.Count == 0)
				{
					Debug.LogWarning("Nothing to reset, pose List is empty");
					return;
				}

				if (targetFrame >= _poseList.Count)
				{
					Debug.LogWarningFormat("exceed target frame({0}) in _poseList({1})", targetFrame, _poseList.Count);
					return;
				}

				if (_targetTransform != null)
				{
					var targetPose = Get(targetFrame);

					_targetTransform.localPosition = targetPose.position;
					_targetTransform.localRotation = targetPose.rotation;

					if (_articulationBody == null)
					{
						_articulationBody = _targetTransform.GetComponent<UE.ArticulationBody>();
					}

					if (_articulationBody != null)
					{
						if (_articulationBody.isRoot)
						{
							_articulationBody.TeleportRoot(targetPose.position, targetPose.rotation);
							_articulationBody.velocity = UE.Vector3.zero;
							_articulationBody.angularVelocity = UE.Vector3.zero;
						}
					}
				}
			}
		}
	}
}
