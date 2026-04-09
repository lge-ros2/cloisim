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
			protected override void ImportJoint(in SDFormat.Joint joint, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);

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

				var articulationBodyChild = linkObjectChild.GetComponent<UE.ArticulationBody>();
				if (articulationBodyChild == null)
				{
					Debug.LogWarningFormat("Link Child has NO Articulation Body, will create an articulation body for linking, Parent({0}) Child({1})",
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
					linkHelper.JointParentLinkName = joint.ParentName;
					linkHelper.JointChildLinkName = joint.ChildName;

					if (joint.Axis != null)
					{
						axis1xyz = joint.Axis.Xyz.ToUnity();
						axisSpringReference = (joint.Type == SDFormat.JointType.Prismatic) ?
							(float)joint.Axis.SpringReference :
							SDF2Unity.CurveOrientation((float)joint.Axis.SpringReference);

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
						if (!double.IsInfinity(joint.Axis.MaxVelocity))
						{
							linkHelper.JointAxisLimitVelocity = (float)joint.Axis.MaxVelocity;
						}
#endif
						if (joint.Axis.Mimic != null)
						{
							linkHelper.JointAxisMimic = joint.Axis.Mimic;
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
						}
					}

					linkHelper.SetJointPoseTarget(axis1xyz, axisSpringReference, axis2xyz, axis2SpringReference);
				}
			}
		}
	}
}