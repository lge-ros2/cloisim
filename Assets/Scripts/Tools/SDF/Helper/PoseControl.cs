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

			struct JointTarget
			{
				public UE.Vector3 axis1xyz;
				public float axis1;
				public UE.Vector3 axis2xyz;
				public float axis2;
			}

			private List<JointTarget> _jointTargetList = new List<JointTarget>();

			public int Count => _poseList.Count;

			public PoseControl(in UE.Transform target)
			{
				_targetTransform = target;
			}

			public void SetJointTarget(
				in UE.Vector3 axis1xyz, in float targetAxis1,
				in UE.Vector3 axis2xyz, in float targetAxis2,
				in int targetFrame = 0)
			{
				var jointTarget = new JointTarget();
				jointTarget.axis1xyz = axis1xyz;
				jointTarget.axis1 = targetAxis1;
				jointTarget.axis2xyz = axis2xyz;
				jointTarget.axis2 = targetAxis2;

				if (targetFrame < _jointTargetList.Count)
				{
					_jointTargetList[targetFrame] = jointTarget;
				}
				else
				{
					lock (_jointTargetList)
					{
						_jointTargetList.Add(jointTarget);
					}
				}
			}

			public void GetJointTarget(
				out UE.Vector3 axis1xyz, out float targetAxis1,
				out UE.Vector3 axis2xyz, out float targetAxis2,
				in int targetFrame = 0)
			{
				if (targetFrame < _jointTargetList.Count)
				{
					var jointTarget = _jointTargetList[targetFrame];
					axis1xyz = jointTarget.axis1xyz;
					targetAxis1 = jointTarget.axis1;
					axis2xyz = jointTarget.axis2xyz;
					targetAxis2 = jointTarget.axis2;
				}
				else
				{
					axis1xyz = UE.Vector3.zero;
					targetAxis1 = 0;
					axis2xyz = UE.Vector3.zero;
					targetAxis2 = 0;
				}
			}

			public void Add(in UE.Vector3 newPosition, in UE.Quaternion newRotation)
			{
				lock (_poseList)
				{
					_poseList.Add(new UE.Pose(newPosition, newRotation));
				}
			}

			public void Set(in UE.Vector3 newPosition, in UE.Quaternion newRotation, in int targetFrame = 0)
			{
				if (targetFrame < _poseList.Count)
				{
					_poseList[targetFrame] = new UE.Pose(newPosition, newRotation);
				}
				else
				{
					Add(newPosition, newRotation);
				}
			}

			public UE.Pose Get(in int targetFrame = 0)
			{
				var getPose = UE.Pose.identity;

				lock (_poseList)
				{
					if (targetFrame < _poseList.Count)
					{
						getPose = _poseList[targetFrame];
					}
				}

				return getPose;
			}

			public void Clear()
			{
				lock (_poseList)
				{
					_poseList.Clear();
				}

				lock (_jointTargetList)
				{
					_jointTargetList.Clear();
				}
			}

			private void ResetArticulationBody(in int targetFrame = 0)
			{
				if (_articulationBody != null)
				{
					_articulationBody.linearVelocity = UE.Vector3.zero;
					_articulationBody.angularVelocity = UE.Vector3.zero;

					var zeroSpace = new UE.ArticulationReducedSpace();
					zeroSpace.dofCount = _articulationBody.dofCount;
					for (var i = 0; i < _articulationBody.dofCount; i++)
					{
						zeroSpace[i] = 0;
					}

					_articulationBody.jointPosition = zeroSpace;
					_articulationBody.jointVelocity = zeroSpace;
					_articulationBody.jointForce = zeroSpace;

					if (_articulationBody.jointType.Equals(UE.ArticulationJointType.FixedJoint))
					{
						return;
					}

					GetJointTarget(out var joint1Axis, out var targetJoint1, out var joint2Axis, out var targetJoint2, targetFrame);
					// Debug.Log($"PoseControl {_articulationBody.name} targetJoint1={targetJoint1.ToString("F6")} targetJoint2={targetJoint2.ToString("F6")}");
					// Debug.Log($"PoseControl {_articulationBody.name} X({_articulationBody.linearLockX}|{_articulationBody.twistLock}) Y({_articulationBody.linearLockY}|{_articulationBody.swingYLock}) Z({_articulationBody.linearLockZ}|{_articulationBody.swingZLock})");

					var xDrive = _articulationBody.xDrive;
					var yDrive = _articulationBody.yDrive;
					var zDrive = _articulationBody.zDrive;

					if (joint1Axis.Equals(UE.Vector3.right) || joint1Axis.Equals(UE.Vector3.left))
					{
						xDrive.target = targetJoint1;
					}
					else if (joint1Axis.Equals(UE.Vector3.up) || joint1Axis.Equals(UE.Vector3.down))
					{
						yDrive.target = targetJoint1;
					}
					else if (joint1Axis.Equals(UE.Vector3.forward) || joint1Axis.Equals(UE.Vector3.back))
					{
						zDrive.target = targetJoint1;
					}

					if (joint2Axis.Equals(UE.Vector3.right) || joint2Axis.Equals(UE.Vector3.left))
					{
						xDrive.target = targetJoint2;
					}
					else if (joint2Axis.Equals(UE.Vector3.up) || joint2Axis.Equals(UE.Vector3.down))
					{
						yDrive.target = targetJoint2;
					}
					else if (joint2Axis.Equals(UE.Vector3.forward) || joint2Axis.Equals(UE.Vector3.back))
					{
						zDrive.target = targetJoint2;
					}

					var isPrismatic = _articulationBody.jointType.Equals(UE.ArticulationJointType.PrismaticJoint);
					if (!isPrismatic)
					{
						xDrive.targetVelocity = 0;
						yDrive.targetVelocity = 0;
						zDrive.targetVelocity = 0;
					}

					_articulationBody.xDrive = xDrive;
					_articulationBody.yDrive = yDrive;
					_articulationBody.zDrive = zDrive;
				}
			}

			public void Reset(in int targetFrame = 0)
			{
				lock (_poseList)
				{
					if (_poseList.Count == 0)
					{
						// Debug.LogWarning("Nothing to reset, pose List is empty");
						return;
					}

					if (targetFrame >= _poseList.Count)
					{
						Debug.LogWarningFormat("exceed target frame({0}) in _poseList({1})", targetFrame, _poseList.Count);
						return;
					}
				}

				if (_targetTransform != null)
				{
					var targetPose = Get(targetFrame);

					if (_articulationBody == null)
					{
						_articulationBody = _targetTransform.GetComponent<UE.ArticulationBody>();
					}

					if (_articulationBody != null)
					{
						if (_articulationBody.isRoot)
						{
							_articulationBody.Sleep();
							_articulationBody.TeleportRoot(targetPose.position, targetPose.rotation);
						}

						ResetArticulationBody(targetFrame);
					}

					_targetTransform.localPosition = targetPose.position;
					_targetTransform.localRotation = targetPose.rotation;

					// Debug.Log($"Reset: {_targetTransform.name} artbody({_articulationBody}) = {targetPose.position} {targetPose.rotation}");
				}
			}
		}
	}
}