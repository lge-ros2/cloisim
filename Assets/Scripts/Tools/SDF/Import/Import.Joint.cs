/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private static UE.Transform FindTransformByName(in string name, UE.Transform targetTransform)
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
					foreach (var transform in targetTransform.GetComponentsInChildren<UE.Transform>())
					{
						if (transform.name.Equals(name))
						{
							foundLinkObject = transform;
							break;
						}
					}
				}
				else
				{
					UE.Transform modelTransform = null;

					foreach (var modelObject in rootTransform.GetComponentsInChildren<SDF.Helper.Model>())
					{
						if (modelObject.name.Equals(modelName))
						{
							modelTransform = modelObject.transform;
							break;
						}
					}

					if (modelTransform != null)
					{
						foreach (var linkObject in modelTransform.GetComponentsInChildren<SDF.Helper.Link>())
						{
							var linkTransform = linkObject.transform;
							if (linkTransform.name.Equals(linkName))
							{
								foundLinkObject = linkTransform;
								break;
							}
						}
					}
				}

				return foundLinkObject;
			}

			protected override void ImportJoint(in Joint joint, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				// Debug.LogFormat("[Joint] {0}, {1} <= {2}", joint.Name, joint.ParentLinkName, joint.ChildLinkName);

				var linkObjectParent = FindTransformByName(joint.ParentLinkName, targetObject.transform);
				var linkObjectChild = FindTransformByName(joint.ChildLinkName, targetObject.transform);

				if (linkObjectParent is null)
				{
					Debug.LogWarningFormat("parent Link object is NULL!!! {0}", joint.ParentLinkName);
					return;
				}

				if (linkObjectChild is null)
				{
					Debug.LogWarningFormat("child Link object is NULL!!! {0}", joint.ChildLinkName);
					return;
				}

				var articulationBodyChild = linkObjectChild.GetComponent<UE.ArticulationBody>();

				if (linkObjectChild is null || linkObjectParent is null)
				{
					Debug.LogWarningFormat("RigidBody of Link is NULL!!! child({0}) parent({1})", linkObjectChild, linkObjectParent);
					return;
				}

				if (articulationBodyChild == null)
				{
					Debug.LogWarningFormat("Link Child has NO Articulation Body, will create an articulation body for linking, parent({0}) child({1})",
						linkObjectParent.name, linkObjectChild.name);
					articulationBodyChild = CreateArticulationBody(linkObjectChild.gameObject);
				}

				var anchorPose = Implement.Joint.SetArticulationBodyRelationship(joint, linkObjectParent, linkObjectChild);

				Implement.Joint.SetArticulationBodyAnchor(articulationBodyChild, anchorPose);

				switch (joint.Type)
				{
					case "ball":
						Implement.Joint.MakeBall(articulationBodyChild);
						break;

					case "prismatic":
						Implement.Joint.MakePrismatic(articulationBodyChild, joint.Axis, joint.Pose);
						break;

					case "revolute":
						Implement.Joint.MakeRevolute(articulationBodyChild, joint.Axis);
						break;

					case "universal":
					case "revolute2":
						Implement.Joint.MakeRevolute2(articulationBodyChild, joint.Axis, joint.Axis2);
						break;

					case "fixed":
						Implement.Joint.MakeFixed(articulationBodyChild);
						break;

					case "gearbox":
						// gearbox_ratio = GetValue<double>("gearbox_ratio");
						// gearbox_reference_body = GetValue<string>("gearbox_reference_body");
						Debug.LogWarning("This type[gearbox] is not supported now.");
						break;

					case "screw":
						// thread_pitch = GetValue<double>("thread_pitch");
						Debug.LogWarning("This type[screw] is not supported now.");
						break;

					default:
						Debug.LogWarningFormat("Check Joint type[{0}]", joint.Type);
						break;
				}

				var linkHelper = linkObjectChild.GetComponent<Helper.Link>();
				if (linkHelper != null)
				{
					var axisSpringReference = 0f;
					var axis2SpringReference = 0f;

					linkHelper.JointName = joint.Name;
					linkHelper.JointParentLinkName = joint.ParentLinkName;
					linkHelper.JointChildLinkName = joint.ChildLinkName;

					if (joint.Axis != null)
					{
						// articulationBodyChild
						linkHelper.JointAxis = SDF2Unity.GetAxis(joint.Axis.xyz);
						if (joint.Axis.dynamics != null)
						{
							if (joint.Type.Equals("prismatic"))
								axisSpringReference = (float)joint.Axis.dynamics.spring_reference;
							else
								axisSpringReference = SDF2Unity.CurveOrientation((float)joint.Axis.dynamics.spring_reference);
						}
					}

					if (joint.Axis2 != null)
					{
						linkHelper.JointAxis2 = SDF2Unity.GetAxis(joint.Axis2.xyz);
						if (joint.Axis2.dynamics != null)
						{
							axis2SpringReference = SDF2Unity.CurveOrientation((float)joint.Axis2.dynamics.spring_reference);
						}
					}

					linkHelper.SetJointTarget(axisSpringReference, axis2SpringReference);

					// set adjusted position for pose control
					var localPosition = linkHelper.transform.localPosition;
					var localRotation = linkHelper.transform.localRotation;
					linkHelper.SetPose(localPosition, localRotation);

					var modelHelper = linkHelper.Model;
					localPosition = modelHelper.transform.localPosition;
					localRotation = modelHelper.transform.localRotation;
					modelHelper.SetPose(localPosition, localRotation);
				}
			}
		}
	}
}