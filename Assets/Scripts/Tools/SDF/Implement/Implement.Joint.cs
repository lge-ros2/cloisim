/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using System.Linq;

namespace SDF
{
	namespace Implement
	{
		public static class Joint
		{
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
					linkParentArticulationBody.useGravity = false;
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

			public static void SetAnchor(this UE.ArticulationBody body, in UE.Pose parentAnchor)
			{
				// UE.Debug.Log(parentAnchor.position);
				body.anchorPosition = parentAnchor.position; //UE.Vector3.zero;
				body.anchorRotation = parentAnchor.rotation;

				// TODO: Consider parentAnchor
				// body.parentAnchorPosition = parentAnchor.position; // TODO: matchAnchors is set to true
				// body.parentAnchorRotation = parentAnchor.rotation; // TODO: matchAnchors is set to true
			}

			public static void MakeRevoluteJoint(this UE.ArticulationBody body, in SDF.Axis axis)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 1.5f;
				body.angularDamping = 2;

				var drive = new UE.ArticulationDrive();

				if (axis.limit.HasJoint())
				{
					// UE.Debug.LogWarningFormat("limit uppper{0}, lower{1}", axis.limit.upper, axis.limit.lower);
					drive.SetRevoluteDriveLimit(axis.limit);
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
					body.jointFriction = 0.5f;
				}

				body.maxJointVelocity = (float)axis.limit.velocity;;

				var jointAxis = SDF2Unity.Axis(axis.xyz);
				// UE.Debug.LogWarning(body.transform.parent.name + "::" + body.name + " = " + jointAxis + " - revolute");

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						body.ReverseAxis(UE.Vector3.forward);
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
						body.ReverseAxis(UE.Vector3.right);
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
						body.ReverseAxis(UE.Vector3.up);
					}
					body.zDrive = drive;
					body.twistLock = UE.ArticulationDofLock.LockedMotion;
					body.swingYLock = UE.ArticulationDofLock.LockedMotion;
					body.swingZLock = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakeRevoluteJoint - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + jointAxis);
				}
			}

			private static void MakeRevoluteJoint2(this UE.ArticulationBody body, in SDF.Axis axis1, in SDF.Axis axis2)
			{
				MakeRevoluteJoint(body, axis1);

				var drive = new UE.ArticulationDrive();

				if (axis2.limit.HasJoint())
				{
					drive.SetRevoluteDriveLimit(axis2.limit);
				}

				body.maxJointVelocity = (float)axis2.limit.velocity;
				drive.forceLimit = (double.IsInfinity(axis2.limit.effort)) ? float.MaxValue : (float)axis2.limit.effort;

				var joint2Axis = SDF2Unity.Axis(axis2.xyz);
				if (joint2Axis.Equals(UE.Vector3.right) || joint2Axis.Equals(UE.Vector3.left))
				{
					if (joint2Axis.Equals(UE.Vector3.left))
					{
						body.ReverseAxis(UE.Vector3.forward);
					}
					body.xDrive = drive;
					body.twistLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.up) || joint2Axis.Equals(UE.Vector3.down))
				{
					if (joint2Axis.Equals(UE.Vector3.down))
					{
						body.ReverseAxis(UE.Vector3.right);
					}
					body.yDrive = drive;
					body.swingYLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else if (joint2Axis.Equals(UE.Vector3.forward) || joint2Axis.Equals(UE.Vector3.back))
				{
					if (joint2Axis.Equals(UE.Vector3.back))
					{
						body.ReverseAxis(UE.Vector3.up);
					}
					body.zDrive = drive;
					body.swingZLock = (axis2.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakeRevoluteJoint2 - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + joint2Axis);
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

			private static void MakeBallJoint(this UE.ArticulationBody body)
			{
				body.jointType = UE.ArticulationJointType.SphericalJoint;
				body.linearDamping = 3;
				body.angularDamping = 1;

				body.swingYLock = UE.ArticulationDofLock.FreeMotion;
				body.swingZLock = UE.ArticulationDofLock.FreeMotion;
				body.twistLock = UE.ArticulationDofLock.FreeMotion;
			}

			private static void MakePrismaticJoint(this UE.ArticulationBody body, in SDF.Axis axis, in SDF.Pose<double> pose)
			{
				body.jointType = UE.ArticulationJointType.PrismaticJoint;
				body.anchorRotation *= SDF2Unity.Rotation(pose?.Rot);
				// body.parentAnchorRotation *= SDF2Unity.Rotation(pose?.Rot);  // TODO: matchAnchors is set to true

				body.linearDamping = 1.5f;
				body.angularDamping = 1;

				var drive = new UE.ArticulationDrive();

				if (axis.limit.HasJoint())
				{
					drive.lowerLimit = (float)axis.limit.lower;
					drive.upperLimit = (float)axis.limit.upper;
				}

				body.maxJointVelocity = (float)axis.limit.velocity;

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
					body.jointFriction = 0.1f;
				}

				var jointAxis = SDF2Unity.Axis(axis.xyz);
				// UE.Debug.LogWarning(body.transform.parent.name + "::" + body.name + " = " + jointAxis + " - Prismatic");

				if (jointAxis.Equals(UE.Vector3.right) || jointAxis.Equals(UE.Vector3.left))
				{
					if (jointAxis.Equals(UE.Vector3.left))
					{
						body.ReverseAxis(UE.Vector3.forward);
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
						body.ReverseAxis(UE.Vector3.right);
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
						body.ReverseAxis(UE.Vector3.up);
					}

					body.zDrive = drive;
					body.linearLockX = UE.ArticulationDofLock.LockedMotion;
					body.linearLockY = UE.ArticulationDofLock.LockedMotion;
					body.linearLockZ = (axis.limit.HasJoint()) ? UE.ArticulationDofLock.LimitedMotion : UE.ArticulationDofLock.FreeMotion;
				}
				else
				{
					UE.Debug.LogWarning("MakePrismaticJoint - Wrong axis, " + body.transform.parent.name + "::" + body.name + " = " + jointAxis);
				}
			}

			public static void MakeJoint(this UE.ArticulationBody body, in SDF.Joint joint)
			{
				switch (joint.Type)
				{
					case "ball":
						body.MakeBallJoint();
						break;

					case "prismatic":
						body.MakePrismaticJoint(joint.Axis, joint.Pose);
						break;

					case "revolute":
						body.MakeRevoluteJoint(joint.Axis);
						break;

					case "universal":
					case "revolute2":
						body.MakeRevoluteJoint2(joint.Axis, joint.Axis2);
						break;

					case "fixed":
						body.MakeFixedJoint();
						break;

					case "gearbox":
						// gearbox_ratio = GetValue<double>("gearbox_ratio");
						// gearbox_reference_body = GetValue<string>("gearbox_reference_body");
						UE.Debug.LogWarning("This type[gearbox] is not supported now.");
						break;

					case "screw":
						// thread_pitch = GetValue<double>("thread_pitch");
						UE.Debug.LogWarning("This type[screw] is not supported now.");
						break;

					default:
						UE.Debug.LogWarningFormat("Check Joint type[{0}]", joint.Type);
						break;
				}
			}

			private static void ReverseAxis(this UE.ArticulationBody body, in UE.Vector3 euler)
			{
				body.anchorRotation *= UE.Quaternion.Euler(euler * 180f);
				// body.parentAnchorRotation *= UE.Quaternion.Euler(euler * 180);  // TODO: matchAnchors is set to true
			}

			private static void SetRevoluteDriveLimit(this ref UE.ArticulationDrive drive, in SDF.Axis.Limit limit)
			{
				drive.lowerLimit = SDF2Unity.CurveOrientation((float)limit.upper);
				drive.upperLimit = SDF2Unity.CurveOrientation((float)limit.lower);
			}

			public static UE.Transform FindTransformByName(this UE.GameObject targetObject, string name)
			{
				return targetObject?.transform.FindTransformByName(name);
			}

			public static UE.Transform FindTransformByName(this UE.Transform targetTransform, string name)
			{
				UE.Transform foundLinkObject = null;

				var rootTransform = targetTransform;

				while (!SDF2Unity.IsRootModel(rootTransform))
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
					var modelHelper = rootTransform.GetComponentsInChildren<SDF.Helper.Model>().FirstOrDefault(x => x.name.Equals(modelName));
					var modelTransform = modelHelper?.transform;

					if (modelTransform != null)
					{
						var foundLinkHelper = modelTransform.GetComponentsInChildren<SDF.Helper.Link>().FirstOrDefault(x => x.transform.name.Equals(linkName));
						foundLinkObject = foundLinkHelper?.transform;
					}
				}

				return foundLinkObject;
			}
		}
	}
}