/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			/// <summary>make root articulation body for handling robots</summary>
			/// <remarks>should add root body first</remarks>
			private void MakeRootArticulationBody(in GameObject targetObject)
			{
				var articulationBody = targetObject.GetComponent<UE.ArticulationBody>();

				// Configure articulation body for root object
				if (articulationBody == null)
				{
					articulationBody = targetObject.AddComponent<UE.ArticulationBody>();
				}

				articulationBody.mass = 0.000000001f;
				articulationBody.useGravity = false;
				articulationBody.immovable = false;
				articulationBody.linearDamping = 0;
				articulationBody.angularDamping = 0;
				articulationBody.ResetCenterOfMass();
				articulationBody.ResetInertiaTensor();
				articulationBody.inertiaTensor = new UE.Vector3(0.000001f, 0.000001f, 0.000001f);
				articulationBody.enabled = true;

				// UE.Debug.Log(targetObject.name + " Create root articulation body");
			}

			protected override System.Object ImportModel(in SDF.Model model, in System.Object parentObject)
			{
				if (model == null)
				{
					return null;
				}

				var targetObject = (parentObject as GameObject);
				var newModelObject = new GameObject(model.Name);
				newModelObject.tag = "Model";

				SetParentObject(newModelObject, targetObject);

				// Apply attributes
				var localPosition = SDF2Unity.GetPosition(model.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(model.Pose.Rot);
				// Debug.Log(newModelObject.name + "::" + localPosition + ", " + localRotation);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.isTopModel = SDF2Unity.CheckTopModel(newModelObject);
				modelHelper.isStatic = model.IsStatic;
				modelHelper.SetPose(localPosition, localRotation);

				if (modelHelper.isTopModel && !modelHelper.isStatic)
				{
					MakeRootArticulationBody(newModelObject);
				}

				return newModelObject as System.Object;
			}
		}
	}
}