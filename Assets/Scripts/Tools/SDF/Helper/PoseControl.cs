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
				lock (poseList)
				{
					poseList.Add(new UE.Pose(newPosition, newRotation));
				}
			}

			public void Set(in UE.Vector3 newPosition, in UE.Quaternion newRotation, in int targetFrame = 0)
			{
				if (targetFrame < poseList.Count)
				{
					poseList[targetFrame] = new UE.Pose(newPosition, newRotation);
				}
				else
				{
					Add(newPosition, newRotation);
				}
			}

			public UE.Pose Get(in int targetFrame = 0)
			{
				var getPose = UE.Pose.identity;

				lock (poseList)
				{
					if (targetFrame < poseList.Count)
					{
						getPose = poseList[targetFrame];
					}
				}

				return getPose;
			}

			public void Clear()
			{
				lock (poseList)
				{
					poseList.Clear();
				}
			}

			private void ResetArticulationBody()
			{
				if (articulationBody != null)
				{
					articulationBody.velocity = UE.Vector3.zero;
					articulationBody.angularVelocity = UE.Vector3.zero;

					var zeroSpace = new UE.ArticulationReducedSpace();
					zeroSpace.dofCount = articulationBody.dofCount;
					for (var i = 0; i < articulationBody.dofCount; i++)
					{
						zeroSpace[i] = 0;
					}

					articulationBody.jointPosition = zeroSpace;
					articulationBody.jointVelocity = zeroSpace;
					articulationBody.jointAcceleration = zeroSpace;
					articulationBody.jointForce = zeroSpace;

					var xDrive = articulationBody.xDrive;
					xDrive.target = 0;
					xDrive.targetVelocity = 0;
					articulationBody.xDrive = xDrive;

					var yDrive = articulationBody.yDrive;
					yDrive.target = 0;
					yDrive.targetVelocity = 0;
					articulationBody.yDrive = yDrive;

					var zDrive = articulationBody.zDrive;
					zDrive.target = 0;
					zDrive.targetVelocity = 0;
					articulationBody.zDrive = zDrive;
				}
			}

			public void Reset(in int targetFrame = 0)
			{
				lock (poseList)
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
				}

				if (targetTransform != null)
				{
					var targetPose = Get(targetFrame);

					if (articulationBody == null)
					{
						articulationBody = targetTransform.GetComponent<UE.ArticulationBody>();
					}

					if (articulationBody != null)
					{
						if (articulationBody.isRoot)
						{
							articulationBody.TeleportRoot(targetPose.position, targetPose.rotation);
						}

						ResetArticulationBody();
					}
					else
					{
						targetTransform.localPosition = targetPose.position;
						targetTransform.localRotation = targetPose.rotation;
					}
				}
			}
		}
	}
}