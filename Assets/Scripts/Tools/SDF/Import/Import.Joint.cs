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
			private static UE.Transform FindTransformByName(in string name, UE.GameObject targetObject)
			{
				UE.Transform foundLinkObject = null;

				var rootTransform = targetObject.transform;

				while (!rootTransform.parent.Equals(targetObject.transform.root))
				{
					rootTransform = rootTransform.parent;
				}

				if (name.Contains("::"))
				{
					var splittedName = name.Replace("::", ":").Split(':');
					if (splittedName.Length == 2)
					{
						UE.Transform modelTransform = null;

						foreach (var modelObject in rootTransform.GetComponentsInChildren<SDF.Helper.Model>())
						{
							if (modelObject.name.Equals(splittedName[0]))
							{
								modelTransform = modelObject.transform;
								break;
							}
						}

						if (modelTransform != null)
						{
							var linkTransform = modelTransform.Find(splittedName[1]);
							if (linkTransform != null)
							{
								foundLinkObject = linkTransform;
							}
						}
					}
				}
				else
				{
					// foreach (var linkObject in rootTransform.GetComponentsInChildren<SDF.Helper.Link>())
					{
						var transform = targetObject.transform.Find(name);
						if (transform != null)
						{
							foundLinkObject = transform;
							// break;
						}
					}
				}

				return foundLinkObject;
			}

			protected override void ImportJoint(in Joint joint, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				// Debug.LogFormat("[Joint] {0}, {1} <= {2}", joint.Name, joint.ParentLinkName, joint.ChildLinkName);

				var linkObjectParent = FindTransformByName(joint.ParentLinkName, targetObject);
				var linkObjectChild = FindTransformByName(joint.ChildLinkName, targetObject);

				if (linkObjectParent is null)
				{
					Debug.LogErrorFormat("parent Link object is NULL!!! {0}", joint.ParentLinkName);
					return;
				}

				if (linkObjectChild is null)
				{
					Debug.LogErrorFormat("child Link object is NULL!!! {0}", joint.ChildLinkName);
					return;
				}

				var articulationBodyChild = linkObjectChild.GetComponent<UE.ArticulationBody>();

				if (linkObjectChild is null || linkObjectParent is null)
				{
					Debug.LogErrorFormat("RigidBody of Link is NULL!!! child({0}) parent({1})", linkObjectChild, linkObjectParent);
					return;
				}
				else
				{
					var modelTransformChild = linkObjectChild.parent;
					var modelTransformParent = linkObjectParent.parent;

					var anchorPose = new UE.Pose();

					if (modelTransformChild.Equals(modelTransformParent))
					{
						linkObjectChild.SetParent(linkObjectParent);

						// Set anchor pose
						anchorPose.position = linkObjectChild.localPosition;
						anchorPose.rotation = linkObjectChild.localRotation;
					}
					else
					{
						modelTransformChild.SetParent(linkObjectParent);

						// Set anchor pose
						anchorPose.position = modelTransformChild.localPosition;
						anchorPose.rotation = modelTransformChild.localRotation;
					}

					Implement.Joint.SetArticulationBodyAnchor(articulationBodyChild, anchorPose);
				}

				switch (joint.Type)
				{
					case "ball":
						Implement.Joint.MakeBall(articulationBodyChild);
						break;

					case "prismatic":
						Implement.Joint.MakePrismatic(articulationBodyChild, joint.Axis, joint.PhysicsODE, joint.Pose);
						break;

					case "revolute":
						Implement.Joint.MakeRevolute(articulationBodyChild, joint.Axis);
						break;

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

				var linkPlugin = linkObjectChild.GetComponent<Helper.Link>();
				if (linkPlugin != null)
				{
					linkPlugin.jointList.Add(joint.Name, articulationBodyChild);
				}
			}
		}
	}
}