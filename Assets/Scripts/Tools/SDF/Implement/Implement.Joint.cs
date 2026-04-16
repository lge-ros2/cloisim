/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using System.Linq;

namespace SDFormat
{
	namespace Implement
	{
		public static class Joint
		{
			public static UE.Pose SetArticulationBodyRelationship(in SDFormat.Joint joint, UE.Transform linkParent, UE.Transform linkChild)
			{
				var modelTransformParent = linkParent.parent;
				var modelTransformChild = linkChild.parent;

				var modelHelperChild = modelTransformChild.GetComponent<SDFormat.Helper.Model>();

				var linkHelperParent = linkParent.GetComponent<SDFormat.Helper.Link>();
				var linkHelperChild = linkChild.GetComponent<SDFormat.Helper.Link>();

				var linkParentArticulationBody = linkParent.GetComponent<UE.ArticulationBody>();
				if (linkParentArticulationBody == null)
				{
					UE.Debug.LogWarningFormat("LinkParent({0}) has no ArticulationBody -> create empty one", linkParent.name);
					linkParentArticulationBody = Import.Loader.CreateArticulationBody(linkParent);
				}

				var anchorPose = new UE.Pose();
				anchorPose.position = UE.Vector3.zero;
				anchorPose.rotation = UE.Quaternion.identity;

				if (linkHelperChild.Model.Equals(linkHelperParent.Model) ||
					modelTransformChild.Equals(modelTransformParent) ||
					modelHelperChild.IsFirstChild)
				{
					linkChild.SetParent(linkParent, false);
				}
				else
				{
					modelTransformChild.SetParent(linkParent, false);
				}

				var jointPosition = joint.RawPose.ToUnityPosition();
				var jointRotation = joint.RawPose.ToUnityRotation();
				anchorPose.position += jointPosition;
				anchorPose.rotation *= jointRotation;

				return anchorPose;
			}

			public static void SetAnchor(this UE.ArticulationBody body, in UE.Pose parentAnchor)
			{
				body.anchorPosition = parentAnchor.position;
				body.anchorRotation = parentAnchor.rotation;
			}

			public static void MakeRevoluteJoint(this UE.ArticulationBody body, in SDFormat.JointAxis axis)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 1.5f;
				body.angularDamping = 2;

				var drive = new UE.ArticulationDrive();

				if (axis.HasJointLimits())
				{
					drive.SetRevoluteDriveLimit(axis);
				}

				drive.forceLimit = (double.IsInfinity(axis.Effort)) ? float.MaxValue : (float)axis.Effort;

				drive.stiffness = (float)axis.SpringStiffness;
				drive.target = SDF2Unity.CurveOrientation((float)axis.SpringReference);
				drive.damping = (float)axis.Damping;

				body.jointFriction = (float)axis.Friction;
				body.maxJointVelocity = (float)axis.MaxVelocity;

				var jointAxis = axis.Xyz.ToUnity().normalized;

				var absX = UE.Mathf.Abs(jointAxis.x);
				var absY = UE.Mathf.Abs(jointAxis.y);
				var absZ = UE.Mathf.Abs(jointAxis.z);

				if (absX >= absY && absX >= absZ)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.right, jointAxis);
					body.xDrive = drive;
					body.twistLock = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else if (absY >= absX && absY >= absZ)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.up, jointAxis);
					body.yDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.swingZLock = UE.ArticulationDofLock.LockedMotion;
				}
				else
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.forward, jointAxis);
					body.zDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			private static void MakeRevoluteJoint2(this UE.ArticulationBody body, in SDFormat.JointAxis axis1, in SDFormat.JointAxis axis2)
			{
				MakeRevoluteJoint(body, axis1);

				var drive = new UE.ArticulationDrive();

				if (axis2.HasJointLimits())
				{
					drive.SetRevoluteDriveLimit(axis2);
				}

				drive.forceLimit = (double.IsInfinity(axis2.Effort)) ? float.MaxValue : (float)axis2.Effort;

				var axis2JointFriction = (float)axis2.Friction;

				body.jointFriction = UE.Mathf.Min(body.jointFriction, axis2JointFriction);
				body.maxJointVelocity = UE.Mathf.Min(body.maxJointVelocity, (float)axis2.MaxVelocity);

				var joint2Axis = axis2.Xyz.ToUnity().normalized;

				var abs2X = UE.Mathf.Abs(joint2Axis.x);
				var abs2Y = UE.Mathf.Abs(joint2Axis.y);
				var abs2Z = UE.Mathf.Abs(joint2Axis.z);

				if (abs2X >= abs2Y && abs2X >= abs2Z)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.right, joint2Axis);
					body.xDrive = drive;
					body.twistLock = (axis2.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (abs2Y >= abs2X && abs2Y >= abs2Z)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.up, joint2Axis);
					body.yDrive = drive;
					body.swingYLock = (axis2.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.forward, joint2Axis);
					body.zDrive = drive;
					body.swingZLock = (axis2.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			private static void MakeFixedJoint(this UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.FixedJoint;
				body.linearDamping = 2;
				body.angularDamping = 2;
				body.jointFriction = 0;
				body.solverIterations = 0;
				body.solverVelocityIterations = 0;
			}

			private static void MakeBallJoint(this UE.ArticulationBody body, in SDFormat.Joint joint)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 3;
				body.angularDamping = 1;

				body.swingYLock = UE.ArticulationDofLock.FreeMotion;
				body.swingZLock = UE.ArticulationDofLock.FreeMotion;
				body.twistLock = UE.ArticulationDofLock.FreeMotion;

				if (joint.Axis != null)
				{
					body.jointFriction = (float)joint.Axis.Friction;
					body.angularDamping = (float)joint.Axis.Damping;
				}
				else if (joint.Element != null && joint.Element.HasElement("dynamics"))
				{
					var dynamics = joint.Element.FindElement("dynamics");
					var friction = dynamics?.FindElement("friction");
					if (friction?.Value != null)
					{
						body.jointFriction = (float)friction.Value.DoubleValue;
					}

					var damping = dynamics?.FindElement("damping");
					if (damping?.Value != null)
					{
						body.angularDamping = (float)damping.Value.DoubleValue;
					}
				}
			}

			private static void MakePrismaticJoint(this UE.ArticulationBody body, in SDFormat.JointAxis axis, in SDFormat.Math.Pose3d pose)
			{
				body.jointType = UE.ArticulationJointType.PrismaticJoint;
				body.anchorRotation *= pose.ToUnityRotation();

				body.linearDamping = 1.5f;
				body.angularDamping = 1;

				var drive = new UE.ArticulationDrive();

				if (axis.HasJointLimits())
				{
					drive.lowerLimit = (float)axis.Lower;
					drive.upperLimit = (float)axis.Upper;
				}

				body.maxJointVelocity = (float)axis.MaxVelocity;

				drive.forceLimit = (double.IsInfinity(axis.Effort)) ? float.MaxValue : (float)axis.Effort;

				drive.stiffness = (float)axis.SpringStiffness;
				drive.target = (float)axis.SpringReference;
				drive.damping = (float)axis.Damping;
				body.jointFriction = (float)axis.Friction;

				var jointAxis = axis.Xyz.ToUnity().normalized;

				var absX = UE.Mathf.Abs(jointAxis.x);
				var absY = UE.Mathf.Abs(jointAxis.y);
				var absZ = UE.Mathf.Abs(jointAxis.z);

				if (absX >= absY && absX >= absZ)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.right, jointAxis);

					body.xDrive = drive;
					body.linearLockX = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else if (absY >= absX && absY >= absZ)
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.up, jointAxis);

					body.yDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
					body.linearLockZ = UE.ArticulationDofLock.LockedMotion;
				}
				else
				{
					body.anchorRotation *= UE.Quaternion.FromToRotation(UE.Vector3.forward, jointAxis);

					body.zDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = (axis.HasJointLimits()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
			}

			public static void MakeJoint(this UE.ArticulationBody body, in SDFormat.Joint joint)
			{
				switch (joint.Type)
				{
					case SDFormat.JointType.Ball:
						body.MakeBallJoint(joint);
						break;

					case SDFormat.JointType.Prismatic:
						body.MakePrismaticJoint(joint.Axis, joint.RawPose);
						break;

					case SDFormat.JointType.Revolute:
					case SDFormat.JointType.Continuous:
						body.MakeRevoluteJoint(joint.Axis);
						break;
					case SDFormat.JointType.Universal:
					case SDFormat.JointType.Revolute2:
						body.MakeRevoluteJoint2(joint.Axis, joint.Axis2);
						break;

					case SDFormat.JointType.Fixed:
						body.MakeFixedJoint();
						break;

					case SDFormat.JointType.Gearbox:
						UE.Debug.LogWarning("This type[gearbox] is not supported now.");
						break;

					case SDFormat.JointType.Screw:
						UE.Debug.LogWarning("This type[screw] is not supported now.");
						break;

					default:
						UE.Debug.LogWarningFormat("Check Joint type[{0}]", joint.Type);
						break;
				}
			}

			private static void SetRevoluteDriveLimit(this ref UE.ArticulationDrive drive, in SDFormat.JointAxis axis)
			{
				drive.lowerLimit = SDF2Unity.CurveOrientation((float)axis.Upper);
				drive.upperLimit = SDF2Unity.CurveOrientation((float)axis.Lower);
			}

			public static UE.Transform FindTransformByName(this UE.GameObject targetObject, string name)
			{
				return targetObject?.transform.FindTransformByName(name);
			}

			public static UE.Transform FindTransformByName(this UE.Transform targetTransform, string name)
			{
				UE.Transform foundLinkObject = null;

				var rootTransform = targetTransform;

				while (!rootTransform.IsRootModel())
				{
					rootTransform = rootTransform.parent;
				}

				(var modelName, var linkName) = SDF2Unity.GetModelLinkName(name, targetTransform.name);
				// UE.Debug.Log("GetModelLinkName  => " + modelName + ", " + linkName);

				if (string.IsNullOrEmpty(modelName))
				{
					// UE.Debug.Log(name + ", Find  => " + targetTransform.name + ", " + rootTransform.name);
					foundLinkObject = targetTransform.GetComponentsInChildren<UE.Transform>().FirstOrDefault(x => x.name.Equals(name));
				}
				else
				{
					var modelHelper = rootTransform.GetComponentsInChildren<SDFormat.Helper.Model>().FirstOrDefault(x => x.name.Equals(modelName));
					var modelTransform = modelHelper?.transform;

					if (modelTransform != null)
					{
						var foundLinkHelper = modelTransform.GetComponentsInChildren<SDFormat.Helper.Link>().FirstOrDefault(x => x.transform.name.Equals(linkName));
						foundLinkObject = foundLinkHelper?.transform;
					}
				}

				return foundLinkObject;
			}
		}
	}
}