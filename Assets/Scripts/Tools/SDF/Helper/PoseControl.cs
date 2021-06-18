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
			private UE.Transform targetTransform = null;
			private UE.ArticulationBody articulationBody = null;

			private List<UE.Pose> poseList = new List<UE.Pose>();
			public int Count => poseList.Count;

			public PoseControl(in UE.Transform target)
			{
				targetTransform = target;
			}

			public void Add(in UE.Vector3 newPosition, in UE.Quaternion newRotation)
			{
				poseList.Add(new UE.Pose(newPosition, newRotation));
			}

			public UE.Pose Get(in int targetFrame = 0)
			{
				return (targetFrame < poseList.Count) ? poseList[targetFrame] : UE.Pose.identity;
			}

			public void ClearPose()
			{
				poseList.Clear();
			}

			public void Reset(in int targetFrame = 0)
			{
				if (poseList.Count == 0)
				{
					// Debug.LogWarning("Nothing to reset, pose List is empty");
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

					targetTransform.localPosition = targetPose.position;
					targetTransform.localRotation = targetPose.rotation;

					if (articulationBody == null)
					{
						articulationBody = targetTransform.GetComponent<UE.ArticulationBody>();
					}

					if (articulationBody != null)
					{
						if (articulationBody.isRoot)
						{
							articulationBody.TeleportRoot(targetPose.position, targetPose.rotation);
							articulationBody.velocity = UE.Vector3.zero;
							articulationBody.angularVelocity = UE.Vector3.zero;
						}
					}
				}
			}
		}
	}
}