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
			private static float minimumInertiaTensor = (float)1e-6;

			private UE.Vector3 GetInertiaTensor(in SDF.Inertial inertia)
			{
				// Debug.LogWarningFormat("GetInertiaTensor: {0}, {1}, {2}", inertia.ixx, inertia.iyy, inertia.izz);
				var inertiaVector = SDF2Unity.GetPosition(inertia.ixx, inertia.iyy, inertia.izz);

				if (inertiaVector.x <= minimumInertiaTensor)
				{
					inertiaVector.x = minimumInertiaTensor;
				}

				if (inertiaVector.y <= minimumInertiaTensor)
				{
					inertiaVector.y = minimumInertiaTensor;
				}

				if (inertiaVector.z <= minimumInertiaTensor)
				{
					inertiaVector.z = minimumInertiaTensor;
				}

				return inertiaVector;
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

					// TODO: NOT Recommended to use innertia values from SDF
					// articulationBody.inertiaTensor = GetInertiaTensor(link.Inertial);
					// articulationBody.inertiaTensorRotation = Quaternion.identity;
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