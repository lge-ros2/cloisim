/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			/// <summary>make root articulation body for handling robots</summary>
			/// <remarks>should add root body first</remarks>
			private void CreateRootArticulationBody(in UE.GameObject targetObject)
			{
				var articulationBody = targetObject.GetComponent<UE.ArticulationBody>();

				// Configure articulation body for root object
				if (articulationBody == null)
				{
					articulationBody = targetObject.AddComponent<UE.ArticulationBody>();
				}

				articulationBody.mass = 0;
				articulationBody.useGravity = false;
				articulationBody.immovable = false;
				articulationBody.linearDamping = 0;
				articulationBody.angularDamping = 0;
#if true
				// TODO: consider to set intertia value manually
				articulationBody.automaticCenterOfMass = true;
				articulationBody.automaticInertiaTensor = true;
#else
				articulationBody.ResetCenterOfMass();
				articulationBody.ResetInertiaTensor();
				articulationBody.inertiaTensor = UE.Vector3.one * MinimumInertiaTensor;
				articulationBody.inertiaTensorRotation = UE.Quaternion.identity;
#endif
				articulationBody.solverIterations = 0;
				articulationBody.solverVelocityIterations = 0;
				articulationBody.velocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.Sleep();
				// UE.Debug.Log(targetObject.name + " Create root articulation body");
			}

			private static void CreateRootRigidBody(in UE.GameObject targetObject)
			{
				var rigidBody = targetObject.GetComponent<UE.Rigidbody>();

				// Configure articulation body for root object
				if (rigidBody == null)
				{
					rigidBody = targetObject.AddComponent<UE.Rigidbody>();
				}

				rigidBody.useGravity = false;
				rigidBody.isKinematic = true;
				rigidBody.mass = 0;
				rigidBody.ResetCenterOfMass();
				rigidBody.ResetInertiaTensor();
				rigidBody.Sleep();
			}

			protected override System.Object ImportModel(in SDF.Model model, in System.Object parentObject)
			{
				if (model == null)
				{
					return null;
				}

				var targetObject = (parentObject as UE.GameObject);
				var newModelObject = new UE.GameObject(model.Name);
				newModelObject.tag = "Model";

				SetParentObject(newModelObject, targetObject);

				// Apply attributes
				var localPosition = SDF2Unity.GetPosition(model.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(model.Pose.Rot);
				// Debug.Log(newModelObject.name + "::" + localPosition + ", " + localRotation);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.isStatic = model.IsStatic;
				modelHelper.SetPose(localPosition, localRotation);
				modelHelper.ResetPose();

				if (modelHelper.IsFirstChild)
				{
					if (modelHelper.isStatic)
					{
						CreateRootRigidBody(newModelObject);
					}
					else
					{
						CreateRootArticulationBody(newModelObject);
					}
				}

				return newModelObject as System.Object;
			}

			protected override void AfterImportModel(in SDF.Model model, in System.Object targetObject)
			{
				var modelObject = (targetObject as UE.GameObject);

				var modelHelper = modelObject.GetComponent<Helper.Model>();
				if (modelHelper.IsFirstChild && !modelHelper.isStatic)
				{
					var childArticulationBodies = modelObject.GetComponentsInChildren<UE.ArticulationBody>();

					if (childArticulationBodies.Length == 1 && childArticulationBodies[0].index == 0)
					{
						// remove root articulation body if there are no ariticulation body in childeren
						UE.GameObject.Destroy(childArticulationBodies[0]);
						modelHelper.hasRootArticulationBody = false;
					}
					else if (childArticulationBodies.Length > 1)
					{
						modelHelper.hasRootArticulationBody = true;
					}

					SegmentationManager.AttachTag(model.OriginalName, modelObject);
				}
			}
		}
	}
}