/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportJoint(in Joint joint, in object parentObject)
			{
				var targetObject = parentObject as UE.GameObject;

				var linkObjectParent = targetObject.FindTransformByName(joint.ParentName);
				var linkObjectChild = targetObject.FindTransformByName(joint.ChildName);

				if (linkObjectParent is null)
				{
					Debug.LogWarningFormat("Parent Link object is NULL!!! {0}", joint.ParentName);
					return;
				}

				if (linkObjectChild is null)
				{
					Debug.LogWarning($"Child Link object is NULL!!! {joint.ChildName}");
					return;
				}

				if (linkObjectChild is null || linkObjectParent is null)
				{
					Debug.LogWarning($"RigidBody of Link is NULL!!! Child({linkObjectChild}) Parent({linkObjectParent})");
					return;
				}

				// A fixed-joint leaf (no actuated joints anywhere below it, e.g. a
				// sensor mount) never moves relative to its parent and has nothing
				// depending on it, so it needs no ArticulationBody at all: dropping it
				// keeps this node out of the 64-node PhysX articulation-island limit.
				// Its colliders keep working as a compound collider on the nearest
				// ArticulationBody ancestor. Its mass/inertia is not folded into the
				// parent, so this is an approximation for links whose mass is small
				// relative to the model.
				//
				// A fixed-joint connector whose subtree has further actuated joints
				// below it (e.g. an end-effector mount) still needs its ArticulationBody
				// kept and chained normally below — there is no supported way to bridge
				// two separate ArticulationBody islands with a classic Joint
				// (Joint.connectedArticulationBody requires a Rigidbody on the joint's
				// own side, not another ArticulationBody), so this case falls through to
				// the normal chaining path further down and still counts toward the
				// 64-node limit.
				if (joint.Type == JointType.Fixed)
				{
					var subtreeHasFurtherArticulation = linkObjectChild.GetComponentsInChildren<UE.ArticulationBody>(true).Length > 1;

					if (!subtreeHasFurtherArticulation)
					{
						var hasSensor = linkObjectChild.GetComponentInChildren<Device>() != null;
						var childArticulationBodyToDrop = linkObjectChild.GetComponent<UE.ArticulationBody>();
						if (childArticulationBodyToDrop != null)
						{
							if (!hasSensor)
							{
								Debug.LogWarning($"[ImportJoint] Dropping ArticulationBody on fixed-joint leaf link '{linkObjectChild.name}' " +
									$"(mass={childArticulationBodyToDrop.mass:F3}) to stay under the articulation-island node limit; its mass is not folded into the parent.");
							}
							UE.Object.DestroyImmediate(childArticulationBodyToDrop);
						}

						// SetArticulationBodyRelationship() is skipped on this path (no
						// ArticulationBody chain to set up), but it is also the only place
						// that reparents the child link's transform under the parent link.
						// Do that reparenting here so the scene hierarchy still reflects
						// the SDF joint's parent/child relationship.
						Implement.Joint.ReparentUnderJointParent(linkObjectParent, linkObjectChild);

						// Frame/TF metadata is independent of ArticulationBody presence:
						// CLOiSimPlugin.ResolvePluginParentFrameName() reads
						// Helper.Link.JointChildLinkName to build TF parent frame ids, so
						// it must still be populated even when we skip the
						// ArticulationBody-based chaining below.
						var droppedLinkHelper = linkObjectChild.GetComponent<Helper.Link>();
						if (droppedLinkHelper != null)
						{
							droppedLinkHelper.JointName = joint.Name;
							droppedLinkHelper.JointParentLinkName = joint.ParentName;
							droppedLinkHelper.JointChildLinkName = joint.ChildName;
						}

						return;
					}
					// else: fall through to normal chaining below.
				}

				var articulationBodyChild = linkObjectChild.GetComponent<UE.ArticulationBody>();
				if (articulationBodyChild == null)
				{
					Debug.LogWarningFormat("Link Child has NO Articulation Body, will create an articulation body for linking, Parent({0}) Child({1})",
						linkObjectParent.name, linkObjectChild.name);
					articulationBodyChild = CreateArticulationBody(linkObjectChild.gameObject);
				}

				var anchorPose = Implement.Joint.SetArticulationBodyRelationship(joint, linkObjectParent, linkObjectChild);

				articulationBodyChild.SetAnchor(anchorPose);

				// SDF frame semantics: a link's pose may be expressed relative_to its
				// own mounting joint (e.g. <link><pose relative_to='some_joint'>).
				// This importer has no GameObject for the joint itself, so fold the
				// joint's own offset (already assumed parent-link-relative here, same
				// assumption SetArticulationBodyRelationship makes above) together
				// with the child's own joint-relative offset into a single
				// parent-relative pose for the child's final local transform below.
				// Scoped to an exact joint-name match only (not empty PoseRelativeTo)
				// so it never touches the ordinary "no explicit pose" case other
				// models rely on via SpecifyPoseAbsolute().
				// Caveat: WorldSaver reads Helper.Base.Pose/PoseRelativeTo to
				// serialize link poses back to SDF, so round-tripping a saved world
				// through this path will lose the original relative_to joint
				// reference (it will be re-saved relative to the parent link with an
				// equivalent absolute offset instead).
				var childLinkHelper = linkObjectChild.GetComponent<Helper.Link>();
				if (childLinkHelper != null && childLinkHelper.PoseRelativeTo == joint.Name)
				{
					var (jointPos, jointRot) = joint.RawPose.ToUnity();
					var (childPos, childRot) = (childLinkHelper.Pose ?? Math.Pose3d.Zero).ToUnity();
					var finalPos = jointPos + jointRot * childPos;
					var finalRot = jointRot * childRot;

					linkObjectChild.localPosition = finalPos;
					linkObjectChild.localRotation = finalRot;
					childLinkHelper.Pose = null;
					childLinkHelper.PoseRelativeTo = null;

					// anchorPosition/anchorRotation must describe the pivot in the
					// CHILD's own local frame (i.e. offset from the child's own
					// origin), not the parent-relative offset used for localPosition
					// above. The child's own declared pose (childPos/childRot) is
					// exactly the joint-to-child offset, so invert it to get the
					// child-to-joint (pivot) offset.
					var invChildRot = UE.Quaternion.Inverse(childRot);
					articulationBodyChild.anchorPosition = invChildRot * -childPos;
					articulationBodyChild.anchorRotation = invChildRot;
				}

				articulationBodyChild.MakeJoint(joint);

				var linkHelper = linkObjectChild.GetComponent<Helper.Link>();
				if (linkHelper != null)
				{
					var axis1xyz = UE.Vector3.zero;
					var axisSpringReference = 0f;
					var axis2xyz = UE.Vector3.zero;
					var axis2SpringReference = 0f;

					linkHelper.JointName = joint.Name;
					linkHelper.JointParentLinkName = joint.ParentName;
					linkHelper.JointChildLinkName = joint.ChildName;

					if (joint.Axis != null)
					{
						axis1xyz = joint.Axis.Xyz.ToUnity();

						// For revolute and other non-prismatic joints, apply curve orientation conversion.
						// Prismatic joints keep spring reference as-is since they do not have rotation semantics.
						if (joint.Type == JointType.Prismatic)
						{
							axisSpringReference = (float)joint.Axis.SpringReference;
						}
						else
						{
							axisSpringReference = SDF2Unity.CurveOrientation((float)joint.Axis.SpringReference);
						}

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
						if (!double.IsInfinity(joint.Axis.MaxVelocity))
						{
							linkHelper.JointAxisLimitVelocity = (float)joint.Axis.MaxVelocity;
						}
#endif
						if (joint.Axis.Mimic != null)
						{
							linkHelper.JointAxisMimic = joint.Axis.Mimic;

							var mimicJoint = linkObjectChild.gameObject.AddComponent<Helper.MimicJoint>();
							mimicJoint.Initialize(joint.Axis.Mimic, joint.Type, articulationBodyChild);
						}
					}

					if (joint.Axis2 != null)
					{
						axis2xyz = joint.Axis2.Xyz.ToUnity();
						axis2SpringReference = SDF2Unity.CurveOrientation((float)joint.Axis2.SpringReference);

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
						if (!double.IsInfinity(joint.Axis2.MaxVelocity))
						{
							linkHelper.JointAxis2LimitVelocity = (float)joint.Axis2.MaxVelocity;
						}
#endif
						if (joint.Axis2.Mimic != null)
						{
							linkHelper.JointAxis2Mimic = joint.Axis2.Mimic;

							var mimicJoint2 = linkObjectChild.gameObject.AddComponent<Helper.MimicJoint>();
							mimicJoint2.Initialize(joint.Axis2.Mimic, joint.Type, articulationBodyChild);
						}
					}

					linkHelper.SetJointPoseTarget(axis1xyz, axisSpringReference, axis2xyz, axis2SpringReference);
				}
			}
		}
	}
}