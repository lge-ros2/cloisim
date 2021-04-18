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
			public static readonly float MinimumInertiaTensor = 1e-6f;

			private static UE.Pose GetInertiaTensor(in SDF.Inertial.Inertia inertia)
			{
				var inertiaMomentum = UE.Pose.identity;
				var inertiaVector = SDF2Unity.GetScalar((float)inertia?.ixx, (float)inertia?.iyy, (float)inertia?.izz);
				var inertiaRotationVector = SDF2Unity.GetScalar((float)inertia?.ixy, (float)inertia?.iyz, (float)inertia?.ixz);

				for (var index = 0; index < 3; index++)
				{
					if (inertiaVector[index] <= MinimumInertiaTensor)
					{
						inertiaVector[index] = MinimumInertiaTensor;
					}

					if (inertiaRotationVector[index] <= MinimumInertiaTensor)
					{
						inertiaRotationVector[index] = MinimumInertiaTensor;
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

				var linkHelper = newLinkObject.AddComponent<Helper.Link>();
				linkHelper.isSelfCollide = link.SelfCollide;
				linkHelper.useGravity = (link.Kinematic) ? false : link.Gravity;
				linkHelper.SetPose(localPosition, localRotation);

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

				var linkHelper = linkObject.GetComponent<Helper.Link>();

				// skip to create articulation body when inertial element is null
				var inertial = link.Inertial;
				if (!linkHelper.Model.isStatic && inertial != null)
				{
					CreateArticulationBody(linkObject, inertial);
				}
				else
				{
					// If the child does not have articulation body, collider of child would disable convex.
					// Sholud be handled after set parent object!!
					var meshColliders = linkObject.GetComponentsInChildren<UE.MeshCollider>();
					foreach (var meshCollider in meshColliders)
					{
						meshCollider.convex = false;
					}
				}
			}

			private static UE.ArticulationBody CreateArticulationBody(in UE.GameObject linkObject, in Inertial inertial = null)
			{
				if (linkObject == null)
				{
					Debug.LogWarning("cannot create articulation body since linkObject is null");
					return null;
				}

				// If the child has collider, collider of child with articulation body would enable convex.
				var meshColliders = linkObject.GetComponentsInChildren<UE.MeshCollider>();
				foreach (var meshCollider in meshColliders)
				{
					meshCollider.convex = true;
				}

				var articulationBody = linkObject.AddComponent<UE.ArticulationBody>();
				var linkHelper = linkObject.GetComponent<SDF.Helper.Link>();

				articulationBody.velocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.useGravity = linkHelper.useGravity;
				articulationBody.jointType = UE.ArticulationJointType.FixedJoint;
				articulationBody.mass = (float)((inertial == null) ? 1e-35f : inertial.mass);

				if (inertial == null)
				{
					articulationBody.ResetCenterOfMass();
				}
				else
				{
					articulationBody.centerOfMass = SDF2Unity.GetPosition(inertial.pose.Pos);
					// Debug.Log(linkObject.name + "  => Center Of Mass: " + articulationBody.centerOfMass.ToString("F6") + ", intertia: " + articulationBody.inertiaTensor.ToString("F6") + ", " + articulationBody.inertiaTensorRotation.ToString("F6"));
				}

				var childMeshCollider = articulationBody.transform.GetComponentInChildren<UE.MeshCollider>();
				if (childMeshCollider != null && childMeshCollider.GetComponentInParent<UE.ArticulationBody>().transform.Equals(articulationBody.transform))
				{
					childMeshCollider.convex = true;

					if (inertial?.inertia != null)
					{
						var momentum = GetInertiaTensor(inertial?.inertia);
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
					articulationBody.inertiaTensor = UE.Vector3.one * MinimumInertiaTensor;
					articulationBody.inertiaTensorRotation = UE.Quaternion.identity;

					Debug.LogWarningFormat(articulationBody.name + " => no mesh collider exists in child");
				}

				// Debug.Log("Create link body " + linkObject.name);

				return articulationBody;
			}
		}
	}
}