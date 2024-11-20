/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportJoint(in Joint joint, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				// Debug.LogFormat("[Joint] {0}, {1} <= {2}", joint.Name, joint.ParentLinkName, joint.ChildLinkName);

				var linkObjectParent = targetObject.FindTransformByName(joint.ParentLinkName);
				var linkObjectChild = targetObject.FindTransformByName(joint.ChildLinkName);

				if (linkObjectParent is null)
				{
					Debug.LogWarningFormat("parent Link object is NULL!!! {0}", joint.ParentLinkName);
					return;
				}

				if (linkObjectChild is null)
				{
					Debug.LogWarning($"child Link object is NULL!!! {joint.ChildLinkName}");
					return;
				}

				if (linkObjectChild is null || linkObjectParent is null)
				{
					Debug.LogWarning($"RigidBody of Link is NULL!!! child({linkObjectChild}) parent({linkObjectParent})");
					return;
				}

				var articulationBodyChild = linkObjectChild.GetComponent<UE.ArticulationBody>();
				if (articulationBodyChild == null)
				{
					Debug.LogWarningFormat("Link Child has NO Articulation Body, will create an articulation body for linking, parent({0}) child({1})",
						linkObjectParent.name, linkObjectChild.name);
					articulationBodyChild = CreateArticulationBody(linkObjectChild.gameObject);
				}

				var anchorPose = Implement.Joint.SetArticulationBodyRelationship(joint, linkObjectParent, linkObjectChild);

				articulationBodyChild.SetAnchor(anchorPose);

				articulationBodyChild.MakeJoint(joint);

				var linkHelper = linkObjectChild.GetComponent<Helper.Link>();
				if (linkHelper != null)
				{
					var axis1xyz = UE.Vector3.zero;
					var axisSpringReference = 0f;
					var axis2xyz = UE.Vector3.zero;
					var axis2SpringReference = 0f;

					linkHelper.JointName = joint.Name;
					linkHelper.JointParentLinkName = joint.ParentLinkName;
					linkHelper.JointChildLinkName = joint.ChildLinkName;

					if (joint.Axis != null)
					{
						if (joint.Axis.dynamics != null)
						{
							axis1xyz = SDF2Unity.Axis(joint.Axis.xyz);
							axisSpringReference = (joint.Type.Equals("prismatic")) ?
							 	(float)joint.Axis.dynamics.spring_reference :
								SDF2Unity.CurveOrientation((float)joint.Axis.dynamics.spring_reference);
						}

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
						if (!double.IsInfinity(joint.Axis.limit.velocity))
						{
							linkHelper.JointAxisLimitVelocity = (float)joint.Axis.limit.velocity;
						}
#endif
					}

					if (joint.Axis2 != null)
					{
						if (joint.Axis2.dynamics != null)
						{
							axis2xyz = SDF2Unity.Axis(joint.Axis2.xyz);
							axis2SpringReference = SDF2Unity.CurveOrientation((float)joint.Axis2.dynamics.spring_reference);
						}

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
						if (!double.IsInfinity(joint.Axis2.limit.velocity))
						{
							linkHelper.JointAxis2LimitVelocity = (float)joint.Axis2.limit.velocity;
						}
#endif
					}

					linkHelper.SetJointPoseTarget(axis1xyz, axisSpringReference, axis2xyz, axis2SpringReference);
				}
			}
		}
	}
}