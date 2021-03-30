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
			private UE.Pose GetInertiaTensor(in SDF.Inertial.Inertia  inertia)
			{
				var inertiaMomentum = UE.Pose.identity;
				var inertiaVector = SDF2Unity.GetScalar((float)inertia.ixx, (float)inertia.iyy, (float)inertia.izz);
				var inertiaRotationVector = SDF2Unity.GetScalar((float)inertia.ixy, (float)inertia.iyz, (float)inertia.ixz);

				const float minimumInertiaTensor = 1e-6f;
				for (var index = 0; index < 3; index++)
				{
					if (inertiaVector[index] <= minimumInertiaTensor)
					{
						inertiaVector[index] = minimumInertiaTensor;
					}

					if (inertiaRotationVector[index] <= minimumInertiaTensor)
					{
						inertiaRotationVector[index] = minimumInertiaTensor;
					}
				}

				inertiaMomentum.position = inertiaVector;
				inertiaMomentum.rotation = UE.Quaternion.Euler(inertiaRotationVector.x, inertiaRotationVector.y, inertiaRotationVector.z);

				return inertiaMomentum;
			}

			protected override System.Object ImportLink(in SDF.Link link, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newLinkObject = new UE.GameObject(link.Name);
				newLinkObject.tag = "Link";

				SetParentObject(newLinkObject, targetObject);

				var localPosition = SDF2Unity.GetPosition(link.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(link.Pose.Rot);

				var linkPlugin = newLinkObject.AddComponent<Helper.Link>();
				linkPlugin.isSelfCollide = link.SelfCollide;
				linkPlugin.SetPose(localPosition, localRotation);

				return newLinkObject as System.Object;
			}

			protected override void AfterImportLink(in SDF.Link link, in System.Object targetObject)
			{
				var linkObject = (targetObject as UE.GameObject);

				if (linkObject == null)
				{
					Debug.LogError("Link Object is null: " + link.Name);
					return;
				}

				// skip to create articulation body when mass is ZERO
				if (link.Inertial != null && link.Inertial.mass != 0)
				{
					var articulationBody = linkObject.AddComponent<UE.ArticulationBody>();

					foreach (var collider in linkObject.GetComponentsInChildren<UE.Collider>())
					{
						if (collider.attachedArticulationBody == null)
						{
							Debug.LogWarningFormat(linkObject.name + " > " + collider.name + " [=] null ArticulationBody ");
						}
					}

					articulationBody.velocity = UE.Vector3.zero;
					articulationBody.angularVelocity = UE.Vector3.zero;
					articulationBody.useGravity = (link.Kinematic)? false:link.Gravity;
					articulationBody.mass = (float)link.Inertial.mass;
					articulationBody.centerOfMass = SDF2Unity.GetPosition(link.Inertial.pose.Pos);
					articulationBody.jointType = UE.ArticulationJointType.FixedJoint;

					var childCollider = articulationBody.transform.GetComponentInChildren<UE.Collider>();
					if (childCollider != null && childCollider.transform.parent.Equals(articulationBody.transform))
					{
						if (link.Inertial.inertia != null)
						{
							var momentum = GetInertiaTensor(link.Inertial.inertia);
							articulationBody.inertiaTensor = momentum.position;
							articulationBody.inertiaTensorRotation = momentum.rotation;
						}
						else
						{
							articulationBody.inertiaTensor = UE.Vector3.one;
							articulationBody.inertiaTensorRotation = UE.Quaternion.identity;
						}
					}
					else
					{
						articulationBody.inertiaTensor = UE.Vector3.one * 1e-6f;
						articulationBody.inertiaTensorRotation = UE.Quaternion.identity;
					}

					// Debug.Log(linkObject.name + "  => Center Of Mass: " + articulationBody.centerOfMass.ToString("F6") + ", intertia: " + articulationBody.inertiaTensor.ToString("F6") + ", " + articulationBody.inertiaTensorRotation.ToString("F6"));
					// Debug.Log("Create link body " + linkObject.name);
				}
				else
				{
					// If the child does not have articulation body, collider of child would disable convex.
					// Sholud be handled after set parent object!!
					var meshColliders = linkObject.GetComponentsInChildren<UE.MeshCollider>();
					foreach (var meshCollider in meshColliders)
					{
						meshCollider.convex = false;
						// Debug.LogWarning("Make convex false:" + meshCollider.name);
					}
				}
			}
		}
	}
}