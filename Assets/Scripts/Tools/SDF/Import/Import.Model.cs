/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			/// <summary>make root articulation body for handling robots</summary>
			/// <remarks>should add root body first</remarks>
			private static UE.ArticulationBody CreateRootArticulationBody(UE.GameObject targetObject)
			{
				var articulationBody = targetObject.GetComponent<UE.ArticulationBody>();

				// Configure articulation body for root object
				if (articulationBody == null)
				{
					articulationBody = targetObject.AddComponent<UE.ArticulationBody>();
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

				// Keep disabled to prevent physics from shifting transforms during import.
				// SpecifyPose() will re-enable all ArticulationBodies after poses are applied.
				articulationBody.enabled = false;

				// UE.Debug.Log(targetObject.name + " Create root articulation body");
				return articulationBody;
			}

			private static UE.Rigidbody CreateRootRigidBody(UE.GameObject targetObject)
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

				return rigidBody;
			}

			private UE.GameObject CreateModel(in Model model, in UE.GameObject parentObject)
			{
				var newModelObject = new UE.GameObject(model.Name)
				{
					tag = "Model"
				};

				parentObject.SetChild(newModelObject);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.modelNameInPath = model.OriginalName();
				modelHelper.isStatic = model.Static;
				modelHelper.Pose = model.RawPose;
				modelHelper.PoseRelativeTo = model.PoseRelativeTo;
				modelHelper.isNested = model.IsNested();

				return newModelObject;
			}

			protected override IEnumerator ImportModel(Model model, System.Object parentObject, Action<System.Object> onCreatedRoot)
			{
				if (model == null)
				{
					yield return null;
				}

				var targetObject = (parentObject as UE.GameObject);
				var newModelObject = CreateModel(model, targetObject);

				ImportLinks(model.Links, newModelObject);

				// Add nested models
				yield return ImportModels(model.Models, newModelObject);

				AfterImportModel(model, newModelObject);

				StoreJoints(model.Joints, newModelObject);

				StorePlugins(model.Plugins, newModelObject);

				if (parentObject == null)
				{
					onCreatedRoot?.Invoke(newModelObject);
				}

				yield return null;
			}

			protected override void AfterImportModel(in Model model, in System.Object targetObject)
			{
				var modelObject = (targetObject as UE.GameObject);

				var modelHelper = modelObject.GetComponent<Helper.Model>();
				if (modelHelper.IsFirstChild)
				{
					Main.SegmentationManager.AttachTag(model.OriginalName(), modelObject);

					// Also attach per-link tags with model::link naming for link-level segmentation
					var linkHelpers = modelObject.GetComponentsInChildren<Helper.Link>();
					foreach (var linkHelper in linkHelpers)
					{
						var linkTagName = model.OriginalName() + "::" + linkHelper.name;
						Main.SegmentationManager.AttachTag(linkTagName, linkHelper.gameObject);
					}

					Main.SegmentationManager.UpdateTags();

					if (modelHelper.isStatic)
					{
						var rb = CreateRootRigidBody(modelObject);
						rb.isKinematic = true;
						rb.useGravity = false;
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