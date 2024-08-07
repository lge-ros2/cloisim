/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Implement
	{
		public class Joint
		{
			private static float DefaultJointFriction = 0.05f;

			public static UE.Pose SetArticulationBodyRelationship(in SDF.Joint joint, UE.Transform linkParent, UE.Transform linkChild)
			{
				var modelTransformParent = linkParent.parent;
				var modelTransformChild = linkChild.parent;

				var modelHelperChild = modelTransformChild.GetComponent<SDF.Helper.Model>();

				var linkHelperParent = linkParent.GetComponent<SDF.Helper.Link>();
				var linkHelperChild = linkChild.GetComponent<SDF.Helper.Link>();

				var linkParentArticulationBody = linkParent.GetComponent<UE.ArticulationBody>();
				if (linkParentArticulationBody == null)
				{
					UE.Debug.LogWarningFormat("LinkParent({0}) has no ArticulationBody -> create empty one", linkParent.name);
					linkParentArticulationBody = Import.Loader.CreateArticulationBody(linkParent);
				}

				var anchorPose = new UE.Pose();
				anchorPose.position = UE.Vector3.zero;
				anchorPose.rotation = UE.Quaternion.identity;

				// link to link within same model
				// model to model
				// model is root model
				if (linkHelperChild.Model.Equals(linkHelperParent.Model) ||
					modelTransformChild.Equals(modelTransformParent) ||
					modelHelperChild.IsFirstChild)
				{
					linkChild.SetParent(linkParent);

					// Set anchor pose
					// anchorPose.position = linkChild.localPosition;
					// UE.Debug.LogWarningFormat("Linking1 ({0}) => ({1})", linkChild.name, linkParent.name);
				}
				else
				{
					modelTransformChild.SetParent(linkParent);

					// Set anchor pose
					// anchorPose.position = modelTransformChild.localPosition;
					// UE.Debug.LogWarningFormat("Linking2 ({0}) => ({1})", modelTransformChild.name, linkParent.name);
				}

				var jointPosition = SDF2Unity.Position(joint.Pose?.Pos);
				var jointRotation = SDF2Unity.Rotation(joint.Pose?.Rot);
				anchorPose.position += jointPosition;
				anchorPose.rotation *= jointRotation;

				return anchorPose;
			}

			public static void SetArticulationBodyAnchor(in UE.ArticulationBody body, in UE.Pose parentAnchor)
			{
				// UE.Debug.Log(parentAnchor.position);
				body.anchorPosition = parentAnchor.position; //UE.Vector3.zero;
				body.anchorRotation = parentAnchor.rotation;

				// TODO: Consider parentAnchor
				// body.parentAnchorPosition = parentAnchor.position; // TODO: matchAnchors is set to true
				// body.parentAnchorRotation = parentAnchor.rotation; // TODO: matchAnchors is set to true
			}

			public static void MakeRevolute(in UE.ArticulationBody body, in SDF.Axis axis)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 0.05f; // TODO : value to find
				body.angularDamping = 0.05f; // TODO : value to find

				var drive = new UE.ArticulationDrive();

				if (axis.limit.HasJoint())
				{
					// UE.Debug.LogWarningFormat("limit uppper{0}, lower{1}", axis.limit.upper, axis.limit.lower);
					SetRevoluteArticulationDriveLimit(ref drive, axis.limit);
				}

				drive.forceLimit = (double.IsInfinity(axis.limit.effort)) ? float.MaxValue : (float)axis.limit.effort;

				if (axis.dynamics != null)
				{
					drive.stiffness = (float)axis.dynamics.spring_stiffness;
					drive.target = SDF2Unity.CurveOrientation((float)axis.dynamics.spring_reference);
					drive.damping = (float)axis.dynamics.damping;
					body.jointFriction = (float)axis.dynamics.friction;
				}
				else
				{
					body.jointFriction = DefaultJointFriction;
				}

				var jointAxis = SDF2Unity.Axis(axis.xyz);
				// UE.Debug.LogWarning(body.transform.parent.name + "::" + body.name + " = " + jointAxis + " - revolute");

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward);
					}
					body.xDrive = drive;
					body.twistLock = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.up) || jointAxis.Equals(UE.Vector3.down))
				{
					if (jointAxis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right);
					}
					body.yDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.forward) || jointAxis.Equals(UE.Vector3.back))
				{
					if (jointAxis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up);
					}
					body.zDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakeRevolute - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + jointAxis);
				}
			}

			public static void MakeRevolute2(in UE.ArticulationBody body, in SDF.Axis axis1, in SDF.Axis axis2)
			{
				MakeRevolute(body, axis1);

				var drive = new UE.ArticulationDrive();

				if (axis2.limit.HasJoint())
				{
					SetRevoluteArticulationDriveLimit(ref drive, axis2.limit);
				}

				drive.forceLimit = (double.IsInfinity(axis2.limit.effort)) ? float.MaxValue : (float)axis2.limit.effort;

				var joint2Axis = SDF2Unity.Axis(axis2.xyz);
				if (joint2Axis.Equals(UE.Vector3.right) || joint2Axis.Equals(UE.Vector3.left))
				{
					if (joint2Axis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward);
					}
					body.xDrive = drive;
					body.twistLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.up) || joint2Axis.Equals(UE.Vector3.down))
				{
					if (joint2Axis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right);
					}
					body.yDrive = drive;
					body.swingYLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.forward) || joint2Axis.Equals(UE.Vector3.back))
				{
					if (joint2Axis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up);
					}
					body.zDrive = drive;
					body.swingZLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakeRevolute2 - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + joint2Axis);
				}
			}

			public static void MakeFixed(in UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.FixedJoint;
				body.linearDamping = 0.00f;
				body.angularDamping = 0.00f;
				body.jointFriction = 0;
			}

			public static void MakeBall(in UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 0.05f;
				body.angularDamping = 0.05f;

				body.swingYLock = UE.ArticulationDofLock.FreeMotion;
				body.swingZLock = UE.ArticulationDofLock.FreeMotion;
				body.twistLock = UE.ArticulationDofLock.FreeMotion;
			}

			public static void MakePrismatic(in UE.ArticulationBody body, in SDF.Axis axis, in SDF.Pose<double> pose)
			{
				body.jointType = UE.ArticulationJointType.PrismaticJoint;
				body.anchorRotation *= SDF2Unity.Rotation(pose?.Rot);
				// body.parentAnchorRotation *= SDF2Unity.Rotation(pose?.Rot);  // TODO: matchAnchors is set to true

				body.linearDamping = 0.05f;
				body.angularDamping = 0.05f;

				var drive = new UE.ArticulationDrive();

				if (axis.limit.HasJoint())
				{
					drive.lowerLimit = (float)axis.limit.lower;
					drive.upperLimit = (float)axis.limit.upper;
				}

				drive.forceLimit = (double.IsInfinity(axis.limit.effort)) ? float.MaxValue : (float)axis.limit.effort;

				if (axis.dynamics != null)
				{
					drive.stiffness = (float)axis.dynamics.spring_stiffness;
					drive.target = (float)axis.dynamics.spring_reference;
					drive.damping = (float)axis.dynamics.damping;
					body.jointFriction = (float)axis.dynamics.friction;
				}
				else
				{
					body.jointFriction = DefaultJointFriction;
				}

				var jointAxis = SDF2Unity.Axis(axis.xyz);
				// UE.Debug.LogWarning(body.transform.parent.name + "::" + body.name + " = " + jointAxis + " - Prismatic");

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.forward);
					}

					body.xDrive = drive;
					body.linearLockX = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.up) || jointAxis.Equals(UE.Vector3.down))
				{
					if (jointAxis.Equals(UE.Vector3.down))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.right);
					}

					body.yDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else if (jointAxis.Equals(UE.Vector3.forward) || jointAxis.Equals(UE.Vector3.back))
				{
					if (jointAxis.Equals(UE.Vector3.back))
					{
						ReverseArticulationBodyAxis(body, UE.Vector3.up);
					}

					body.zDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakePrismatic - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + jointAxis);
				}
			}

			private static void ReverseArticulationBodyAxis(in UE.ArticulationBody body, in UE.Vector3 euler)
			{
				body.anchorRotation *= UE.Quaternion.Euler(euler * 180f);
				// body.parentAnchorRotation *= UE.Quaternion.Euler(euler * 180);  // TODO: matchAnchors is set to true
			}

			private static void SetRevoluteArticulationDriveLimit(ref UE.ArticulationDrive drive, in SDF.Axis.Limit limit)
			{
				drive.lowerLimit = SDF2Unity.CurveOrientation((float)limit.upper);
				drive.upperLimit = SDF2Unity.CurveOrientation((float)limit.lower);
			}
		}
	}
}