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

			struct JointTarget
			{
				public float axis1;
				public float axis2;
			}

			private List<JointTarget> jointTargetList = new List<JointTarget>();

			public int Count => poseList.Count;

			public PoseControl(in UE.Transform target)
			{
				targetTransform = target;
			}

			public void SetJointTarget(in float targetAxis1, in float targetAxis2, in int targetFrame = 0)
			{
				var jointTarget = new JointTarget();
				jointTarget.axis1 = targetAxis1;
				jointTarget.axis2 = targetAxis2;

				if (targetFrame < jointTargetList.Count)
				{
					jointTargetList[targetFrame] = jointTarget;
				}
				else
				{
					lock (jointTargetList)
					{
						jointTargetList.Add(jointTarget);
					}
				}
			}

			public void GetJointTarget(out float targetAxis1, out float targetAxis2, in int targetFrame = 0)
			{
				targetAxis1 = 0;
				targetAxis2 = 0;

				if (targetFrame < jointTargetList.Count)
				{
					var jointTarget = jointTargetList[targetFrame];
					targetAxis1 = jointTarget.axis1;
					targetAxis2 = jointTarget.axis2;
				}
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

				lock (jointTargetList)
				{
					jointTargetList.Clear();
				}
			}

			private void ResetArticulationBody(in int targetFrame = 0)
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
					var isPrismatic = articulationBody.jointType.Equals(UE.ArticulationJointType.PrismaticJoint);

					GetJointTarget(out var targetJoint1, out var targetJoint2, targetFrame);

					var xDrive = articulationBody.xDrive;
					if (!isPrismatic)
					{
						xDrive.targetVelocity = 0;
					}
					if (!articulationBody.linearLockX.Equals(UE.ArticulationDofLock.LockedMotion) ||
						!articulationBody.twistLock.Equals(UE.ArticulationDofLock.LockedMotion))
					{
						if (!articulationBody.swingYLock.Equals(UE.ArticulationDofLock.LockedMotion) ||
							!articulationBody.swingZLock.Equals(UE.ArticulationDofLock.LockedMotion))
						{
							xDrive.target = targetJoint2;
						}
						else
						{
							xDrive.target = targetJoint1;
						}
					}
					else
					{
						xDrive.target = 0;
					}
					articulationBody.xDrive = xDrive;

					var yDrive = articulationBody.yDrive;
					if (!isPrismatic)
					{
						yDrive.targetVelocity = 0;
					}
					if (!articulationBody.linearLockY.Equals(UE.ArticulationDofLock.LockedMotion) ||
						!articulationBody.swingYLock.Equals(UE.ArticulationDofLock.LockedMotion))
					{
						if (!articulationBody.twistLock.Equals(UE.ArticulationDofLock.LockedMotion) ||
							!articulationBody.swingZLock.Equals(UE.ArticulationDofLock.LockedMotion))
						{
							yDrive.target = targetJoint2;
						}
						else
						{
							yDrive.target = targetJoint1;
						}
					}
					else
					{
						yDrive.target = 0;
					}
					articulationBody.yDrive = yDrive;

					var zDrive = articulationBody.zDrive;
					if (!isPrismatic)
					{
						zDrive.targetVelocity = 0;
					}
					if (!articulationBody.linearLockY.Equals(UE.ArticulationDofLock.LockedMotion) ||
						!articulationBody.swingZLock.Equals(UE.ArticulationDofLock.LockedMotion))
					{
						if (!articulationBody.twistLock.Equals(UE.ArticulationDofLock.LockedMotion) ||
							!articulationBody.swingYLock.Equals(UE.ArticulationDofLock.LockedMotion))
						{
							zDrive.target = targetJoint2;
						}
						else
						{
							zDrive.target = targetJoint1;
						}
					}
					else
					{
						zDrive.target = 0;
					}
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

						ResetArticulationBody(targetFrame);
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