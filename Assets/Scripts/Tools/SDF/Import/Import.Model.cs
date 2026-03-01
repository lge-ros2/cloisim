/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
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
					// Temporarily deactivate to prevent PhysX error:
					// "PxArticulationLink::release(): root link may not be released while articulation is in a scene"
					var wasActive = targetObject.activeSelf;
					targetObject.SetActive(false);
					articulationBody = targetObject.AddComponent<UE.ArticulationBody>();
					targetObject.SetActive(wasActive);
				}

				articulationBody.useGravity = false;
				articulationBody.immovable = false;
				articulationBody.linearDamping = 3;
				articulationBody.angularDamping = 1;
				articulationBody.jointFriction = 0;

				articulationBody.mass = 0.5f;
				articulationBody.automaticCenterOfMass = false;
				articulationBody.ResetCenterOfMass();
				articulationBody.centerOfMass = UE.Vector3.zero;

				articulationBody.automaticInertiaTensor = true;
				articulationBody.ResetInertiaTensor();

				articulationBody.solverIterations = 0;
				articulationBody.solverVelocityIterations = 0;
				articulationBody.linearVelocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.sleepThreshold = 0.01f;
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
				var localPosition = model.Pose?.Pos.ToUnity() ?? UE.Vector3.zero;
				var localRotation = model.Pose?.Rot.ToUnity() ?? UE.Quaternion.identity;
				// UE.Debug.Log(newModelObject.name + "::" + localPosition + ", " + localRotation);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.modelNameInPath = model.OriginalName;
				modelHelper.isStatic = model.IsStatic;
				modelHelper.Pose = model?.Pose;
				modelHelper.isNested = model.IsNested;

				return newModelObject;
			}

			protected override IEnumerator ImportModel(SDF.Model model, System.Object parentObject, Action<System.Object> onCreatedRoot)
			{
				if (model == null)
				{
					yield return null;
				}

				// UE.Debug.Log("ImportModel({0})", model.Name);

				var targetObject = (parentObject as UE.GameObject);
				var newModelObject = CreateModel(model, targetObject);

				ImportLinks(model.GetLinks(), newModelObject);

				// Add nested models
				yield return ImportModels(model.GetModels(), newModelObject);

				AfterImportModel(model, newModelObject);

				StoreJoints(model.GetJoints(), newModelObject);

				StorePlugins(model.GetPlugins(), newModelObject);

				if (parentObject == null)
				{
					onCreatedRoot?.Invoke(newModelObject);
				}

				yield return null;
			}

			protected override void AfterImportModel(in SDF.Model model, in System.Object targetObject)
			{
				var modelObject = (targetObject as UE.GameObject);

				var modelHelper = modelObject.GetComponent<Helper.Model>();
				if (modelHelper.IsFirstChild)
				{
					Main.SegmentationManager.AttachTag(model.OriginalName, modelObject);
					Main.SegmentationManager.UpdateTags();

					if (modelHelper.isStatic)
					{
						CreateRootRigidBody(modelObject);
					}
					else
					{
						var bodies = modelHelper.GetComponentsInChildren<UE.ArticulationBody>(true);
						if (bodies.Length > 0)
							CreateRootArticulationBody(modelObject);
						else
							UE.Debug.LogWarning($"'{modelHelper.name}' has no articulation bodies in children");
					}
				}
			}
		}
	}
}