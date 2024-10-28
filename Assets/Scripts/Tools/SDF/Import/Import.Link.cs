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
				/**
				 *  | Ixx  Ixy  Ixz |
				 *  | Ixy  Iyy  Iyz |
				 *  | Ixz  Iyz  Izz |
				 */
				var inertiaMomentum = UE.Pose.identity;
				var inertiaVector = SDF2Unity.Scalar((float)inertia?.ixx, (float)inertia?.iyy, (float)inertia?.izz);
				var inertiaRotationVector = SDF2Unity.Scalar((float)inertia?.ixy, (float)inertia?.iyz, (float)inertia?.ixz);

				for (var index = 0; index < 3; index++)
				{
					if (inertiaVector[index] <= MinimumInertiaTensor)
					{
						inertiaVector[index] = MinimumInertiaTensor;
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

				targetObject.SetChild(newLinkObject);

				var linkHelper = newLinkObject.AddComponent<Helper.Link>();
				linkHelper.isSelfCollide = link.SelfCollide;
				linkHelper.useGravity = (link.Kinematic) ? false : link.Gravity;
				linkHelper.Pose = link?.Pose;

				if (link.Battery != null)
				{
					linkHelper.AttachBattery(link.Battery.name, (float)link.Battery.voltage);
				}

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
					Loader.CreateArticulationBody(linkObject, inertial);
				}
			}

			public static UE.ArticulationBody CreateArticulationBody(in UE.Transform linkObjectTransform, in Inertial inertial = null)
			{
				return CreateArticulationBody(linkObjectTransform.gameObject, inertial);
			}

			private static UE.ArticulationBody CreateArticulationBody(in UE.GameObject linkObject, in Inertial inertial = null)
			{
				if (linkObject == null)
				{
					Debug.LogWarning("cannot create articulation body since linkObject is null");
					return null;
				}

				var linkHelper = linkObject.GetComponent<SDF.Helper.Link>();
				var colliders = linkObject.GetComponentsInChildren<UE.Collider>();

				// handling mesh collider
				foreach (var collider in colliders)
				{
					var meshCollider = collider as UE.MeshCollider;

					if (meshCollider != null)
					{
						meshCollider.convex = true;
					}
				}

				var articulationBody = linkObject.AddComponent<UE.ArticulationBody>();

				articulationBody.velocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.useGravity = (linkHelper == null) ? false : linkHelper.useGravity;
				articulationBody.jointType = UE.ArticulationJointType.FixedJoint;
				articulationBody.mass = (inertial == null) ? 0.5f : (float)inertial.mass;
				articulationBody.linearDamping = 3;
				articulationBody.angularDamping = 1;
				articulationBody.jointFriction = 0f;

				articulationBody.solverIterations = 0;
				articulationBody.solverVelocityIterations = 0;
				articulationBody.velocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.sleepThreshold = 1f;
				articulationBody.Sleep();

				articulationBody.matchAnchors = true;
				articulationBody.anchorPosition = UE.Vector3.zero;
				articulationBody.anchorRotation = UE.Quaternion.identity;

				articulationBody.ResetCenterOfMass();
				if (inertial?.pose != null)
				{
					articulationBody.centerOfMass = SDF2Unity.Position(inertial.pose?.Pos);
					articulationBody.automaticCenterOfMass = false;
				}
				else
				{
					articulationBody.automaticCenterOfMass = true;
				}
				// Debug.Log(linkObject.name + "  => Center Of Mass: " + articulationBody.centerOfMass.ToString("F6") + ", intertia: " + articulationBody.inertiaTensor.ToString("F6") + ", " + articulationBody.inertiaTensorRotation.ToString("F6"));

				if (colliders.Length == 0)
				{
					Debug.LogWarningFormat(articulationBody.name + " => no mesh collider exists in child");
				}

				articulationBody.ResetInertiaTensor();
				if (inertial?.inertia != null)
				{
					var momentum = GetInertiaTensor(inertial?.inertia);
					Debug.Log(momentum);
					articulationBody.inertiaTensor = momentum.position;
					articulationBody.inertiaTensorRotation = momentum.rotation;
					articulationBody.automaticInertiaTensor = false;
				}
				else
				{
					articulationBody.automaticInertiaTensor = true;
				}

				Debug.Log("Create link body " + linkObject.name);
				return articulationBody;
			}
		}
	}
}