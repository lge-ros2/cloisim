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
			private static readonly float MinimumInertiaTensor = 1e-6f;

			private static UE.Pose GetInertiaTensor(in SDF.Inertial.Inertia inertia, in UE.ArticulationBody tempArticulationBodyForCalculation)
			{
				/**
				 *  Inertia Tensor
				 *  TBD for Ixy, Ixz, Iyz
				 *  | Ixx  Ixy  Ixz |
				 *  | Ixy  Iyy  Iyz |
				 *  | Ixz  Iyz  Izz |
				 */
				var inertiaMomentum = UE.Pose.identity;
				var inertiaVector = SDF2Unity.Scalar((float)inertia?.ixx, (float)inertia?.iyy, (float)inertia?.izz);

				for (var index = 0; index < 3; index++)
				{
					if (inertiaVector[index] <= MinimumInertiaTensor)
					{
						inertiaVector[index] = MinimumInertiaTensor;
					}
				}

				/*
				 *  Unity’s ArticulationBody does not directly expose off-diagonal components of the inertia tensor(Ixy, Ixz, Iyz).
				 *  If these are needed, you might have to approximate them by adjusting inertiaTensorRotation,
				 *  which changes the orientation of the inertia tensor.
				 */
				// var inertiaRotationVector = SDF2Unity.Scalar((float)inertia?.ixy, (float)inertia?.iyz, (float)inertia?.ixz);

				inertiaMomentum.position = inertiaVector;
				// inertiaMomentum.rotation = UE.Quaternion.Euler(inertiaRotationVector.x, inertiaRotationVector.y, inertiaRotationVector.z);

				#region Temporary Code for intertia tensor rotation
				tempArticulationBodyForCalculation.automaticInertiaTensor = true;
				// UE.Debug.LogWarning($"{tempArticulationBodyForCalculation.name} Inertia Tensor: {tempArticulationBodyForCalculation.inertiaTensor}, {tempArticulationBodyForCalculation.inertiaTensorRotation.eulerAngles}");
				inertiaMomentum.rotation = tempArticulationBodyForCalculation.inertiaTensorRotation;
				tempArticulationBodyForCalculation.automaticInertiaTensor = false;
				#endregion

				// Debug.Log("Inertia Tensor: " + inertiaMomentum.position + ", " + inertiaMomentum.rotation.eulerAngles);
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

				// skip to create articulation body when inertial element is null
				var modelHelpers = linkObject.GetComponentsInParent<Helper.Model>();

				var hasStaticModel = false;
				for (var i = 0; i < modelHelpers.Length; i++)
				{
					var modelHelper = modelHelpers[i];
					// Debug.LogWarning($"AfterImportLink: {linkObject.name} {modelHelper.name} {link.Name} {modelHelper.isStatic} {modelHelper.gameObject.isStatic}");
					if (modelHelper.isStatic)
					{
						hasStaticModel = true;
					}

					if (modelHelper.IsFirstChild || hasStaticModel)
					{
						break;
					}
				}

				var inertial = link.Inertial;
				if (!hasStaticModel && inertial != null)
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
					articulationBody.centerOfMass = inertial.pose?.Pos.ToUnity() ?? UE.Vector3.zero;
					articulationBody.automaticCenterOfMass = false;
				}
				else
				{
					articulationBody.automaticCenterOfMass = true;
				}
				// Debug.Log($"{linkObject.name} => Center Of Mass: {articulationBody.centerOfMass.ToString("F5")} | intertia: {articulationBody.inertiaTensor.ToString("F5")}, {articulationBody.inertiaTensorRotation.ToString("F5")}");

				if (colliders.Length == 0)
				{
					Debug.LogWarning($"{articulationBody.name} => no mesh collider exists in child");
				}

				articulationBody.ResetInertiaTensor();
				if (inertial?.inertia != null)
				{
					var momentum = GetInertiaTensor(inertial?.inertia, articulationBody);
					articulationBody.inertiaTensor = momentum.position;
					articulationBody.inertiaTensorRotation = momentum.rotation;
					articulationBody.automaticInertiaTensor = false;
				}
				else
				{
					articulationBody.automaticInertiaTensor = true;
				}

				// Debug.Log("Create link body " + linkObject.name);
				return articulationBody;
			}
		}
	}
}