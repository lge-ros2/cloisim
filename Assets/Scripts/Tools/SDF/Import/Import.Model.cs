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

				articulationBody.useGravity = false;
				articulationBody.immovable = false;
				articulationBody.linearDamping = 0.05f;
				articulationBody.angularDamping = 0.05f;
				articulationBody.jointFriction = 0;

				articulationBody.ResetCenterOfMass();
				articulationBody.mass = 1e-07f;
				articulationBody.automaticCenterOfMass = false;

				articulationBody.ResetInertiaTensor();
				articulationBody.automaticInertiaTensor = false;
				articulationBody.inertiaTensor = UE.Vector3.one * MinimumInertiaTensor;
				articulationBody.inertiaTensorRotation = UE.Quaternion.identity;

				articulationBody.solverIterations = 0;
				articulationBody.solverVelocityIterations = 0;
				articulationBody.velocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;

				articulationBody.sleepThreshold = 0.1f;
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

			private UE.GameObject CreateModel(in SDF.Model model, in UE.GameObject parentObject)
			{
				var newModelObject = new UE.GameObject(model.Name);
				newModelObject.tag = "Model";

				parentObject.SetChild(newModelObject);

				// Apply attributes
				var localPosition = SDF2Unity.Position(model.Pose?.Pos);
				var localRotation = SDF2Unity.Rotation(model.Pose?.Rot);
				// UE.Debug.Log(newModelObject.name + "::" + localPosition + ", " + localRotation);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.modelNameInPath = model.OriginalName;
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
				return newModelObject;
			}


			protected override System.Object ImportModel(in SDF.Model model, in System.Object parentObject)
			{
				if (model == null)
				{
					return null;
				}

				var targetObject = (parentObject as UE.GameObject);
				var newModelObject = CreateModel(model, targetObject);

				ImportLinks(model.GetLinks(), newModelObject);

				// Add nested models
				ImportModels(model.GetModels(), newModelObject);

				AfterImportModel(model, newModelObject);

				ImportJoints(model.GetJoints(), newModelObject);

				ImportPlugins(model.GetPlugins(), newModelObject);

				return newModelObject as System.Object;
			}

			protected override void AfterImportModel(in SDF.Model model, in System.Object targetObject)
			{
				var modelObject = (targetObject as UE.GameObject);

				var modelHelper = modelObject.GetComponent<Helper.Model>();
				if (modelHelper.IsFirstChild)
				{
					// UE.Debug.Log("AfterImportModel: " + model.OriginalName + ", " + modelObject.name);
					Main.SegmentationManager.AttachTag(model.OriginalName, modelObject);
					Main.SegmentationManager.UpdateTags();
				}
			}
		}
	}
}