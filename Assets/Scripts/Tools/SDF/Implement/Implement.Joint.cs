/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	public partial class Implement
	{
		public class Joint
		{
			public static void SetArticulationBodyAnchor(in UE.ArticulationBody body, in UE.Pose parentAnchor)
			{
				if (body == null)
				{
					Debug.LogWarning("Articulation Body is NULL");
					return;
				}

				body.anchorPosition = UE.Vector3.zero;
				body.anchorRotation = UE.Quaternion.identity;
				body.parentAnchorPosition = parentAnchor.position;
				body.parentAnchorRotation = parentAnchor.rotation;
			}

			public static void MakeRevolute(in UE.ArticulationBody body, in SDF.Axis axis)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 0;
				body.angularDamping = 0;

				if (axis.dynamics != null)
				{
					body.jointFriction = (float)axis.dynamics.friction;
				}
				else
				{
					body.jointFriction = 0;
				}

				var drive = new UE.ArticulationDrive();

				if (axis.limit.Use())
				{
					SetRevoluteArticulationDriveLimit(ref drive, axis.limit);
				}

				drive.forceLimit = float.PositiveInfinity;

				var jointAxis = SDF2Unity.GetAxis(axis.xyz);

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward * 180);
					}
					body.xDrive = drive;
					body.twistLock = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.up) || jointAxis.Equals(UE.Vector3.down))
				{
					if (jointAxis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right * 180);
					}
					body.yDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.forward) || jointAxis.Equals(UE.Vector3.back))
				{
					if (jointAxis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up * 180);
					}
					body.zDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			public static void MakeRevolute2(in UE.ArticulationBody body, in SDF.Axis axis1, in SDF.Axis axis2)
			{
				MakeRevolute(body, axis1);

				var drive = new UE.ArticulationDrive();

				if (axis2.limit.Use())
				{
					SetRevoluteArticulationDriveLimit(ref drive, axis2.limit);
				}

				drive.forceLimit = float.PositiveInfinity;

				var joint2Axis = SDF2Unity.GetAxis(axis2.xyz);
				if (joint2Axis.Equals(UE.Vector3.right) || joint2Axis.Equals(UE.Vector3.left))
				{
					if (joint2Axis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward * 180);
					}
					body.xDrive = drive;
					body.twistLock = (axis2.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.up) || joint2Axis.Equals(UE.Vector3.down))
				{
					if (joint2Axis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right * 180);
					}
					body.yDrive = drive;
					body.swingYLock = (axis2.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.forward) || joint2Axis.Equals(UE.Vector3.back))
				{
					if (joint2Axis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up * 180);
					}
					body.zDrive = drive;
					body.swingZLock = (axis2.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			public static void MakeFixed(in UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.FixedJoint;
				body.linearDamping = 0;
				body.angularDamping = 0;
				body.jointFriction = 0;
			}

			public static void MakeBall(in UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 0;
				body.angularDamping = 0;

				body.swingYLock = UE.ArticulationDofLock.FreeMotion;
				body.swingZLock = UE.ArticulationDofLock.FreeMotion;
				body.twistLock = UE.ArticulationDofLock.FreeMotion;
			}

			public static void MakePrismatic(in UE.ArticulationBody body, in SDF.Axis axis, in SDF.Joint.Physics.ODE physicsInfo, in SDF.Pose<double> pose)
			{
				body.jointType = UE.ArticulationJointType.PrismaticJoint;
				body.parentAnchorRotation *= SDF2Unity.GetRotation(pose.Rot);

				body.linearDamping = 0;
				body.angularDamping = 0;

				var drive = new UE.ArticulationDrive();

				if (axis.limit.Use())
				{
					// Debug.LogWarningFormat("limit uppper{0}, lower{1}", axis.limit.upper, axis.limit.lower);
					drive.lowerLimit = (float)(axis.limit.lower);
					drive.upperLimit = (float)(axis.limit.upper);
				}

				drive.forceLimit = (float)physicsInfo.max_force;

				if (axis.dynamics != null)
				{
					drive.stiffness = (float)axis.dynamics.spring_stiffness;
					drive.damping = (float)axis.dynamics.damping;
					body.jointFriction = (float)axis.dynamics.friction;
				}

				var jointAxis = SDF2Unity.GetAxis(axis.xyz);

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward * 180);
					}

					body.xDrive = drive;
					body.linearLockX = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.up) || jointAxis.Equals(UE.Vector3.down))
				{
					if (jointAxis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right * 180);
					}

					body.yDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.forward) || jointAxis.Equals(UE.Vector3.back))
				{
					if (jointAxis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up * 180);
					}
					body.zDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = (axis.limit.Use()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			private static void ReverseArticulationBodyAxis(in UE.ArticulationBody body, in UE.Vector3 euler)
			{
				body.anchorRotation *= UE.Quaternion.Euler(euler);
				body.parentAnchorRotation *= UE.Quaternion.Euler(euler);
			}

			private static void SetRevoluteArticulationDriveLimit(ref UE.ArticulationDrive drive, in SDF.Axis.Limit limit)
			{
				drive.lowerLimit = -(float)limit.upper * UE.Mathf.Rad2Deg;
				drive.upperLimit = -(float)limit.lower * UE.Mathf.Rad2Deg;
			}
		}
	}
}