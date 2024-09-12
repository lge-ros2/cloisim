/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Linq;
using System;
using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public static class Util
		{
			private static UE.GameObject _rootModels = null;

			public static UE.GameObject RootModels
			{
				get => _rootModels;
				set => _rootModels = value;
			}

			public static void SetChild(this UE.GameObject parent, in UE.GameObject child)
			{
				child.transform.position = UE.Vector3.zero;
				child.transform.rotation = UE.Quaternion.identity;

				var targetParentTransform = (parent == null) ? _rootModels.transform : parent.transform;
				child.transform.SetParent(targetParentTransform, false);

				child.transform.localScale = UE.Vector3.one;
				child.transform.localPosition = UE.Vector3.zero;
				child.transform.localRotation = UE.Quaternion.identity;
			}

			private static UE.Transform FindRootParentModel(SDF.Helper.Base targetBaseHelper)
			{
				var foundRootModelTransform = targetBaseHelper.RootModel.transform;

				if (targetBaseHelper as SDF.Helper.Model)
				{
					// UE.Debug.Log($"Is Model helper {targetBaseHelper.name}");
					for (var parentTransform = targetBaseHelper.transform.parent;
						parentTransform != null;
						parentTransform = parentTransform.parent)
					{
						var modelHelper = parentTransform?.GetComponent<SDF.Helper.Model>();
						if (modelHelper != null)
						{
							if (!modelHelper.isNested)
							{
								// UE.Debug.Log($"FindRootModel: modelHelper  {modelHelper.name} {modelHelper.isNested} {modelHelper.name}");
								foundRootModelTransform = modelHelper.transform;
								break;
							}
						}
					}
				}
				else if (targetBaseHelper as SDF.Helper.Link)
				{
					// UE.Debug.Log($"Is not Model helper {targetBaseHelper.name}");
					for (var parentTransform = targetBaseHelper.transform.parent;
						parentTransform != null;
						parentTransform = parentTransform.parent)
					{
						var linkHelper = parentTransform?.GetComponent<SDF.Helper.Link>();
						if (linkHelper != null)
						{
							if (linkHelper.Model.isNested)
							{
								// UE.Debug.Log($"FindRootModel: {linkHelper.Model.name} {linkHelper.Model.isNested} {linkHelper.name}");
								foundRootModelTransform = linkHelper.transform;
								break;
							}
						}
					}
				}
				else // SDF.Helper.Collision or SDF.Helper.Visual
				{
					for (var parentTransform = targetBaseHelper.transform.parent;
						parentTransform != null;
						parentTransform = parentTransform.parent)
					{
						var linkHelper = parentTransform?.GetComponent<SDF.Helper.Link>();
						if (linkHelper != null)
						{
							// UE.Debug.Log($"FindRootModel: {linkHelper.Model.name} {linkHelper.Model.isNested} {linkHelper.name}");
							foundRootModelTransform = linkHelper.transform;
							break;
						}
					}
				}

				return foundRootModelTransform;
			}

			public static void SpecifyPose(this Object targetObject)
			{
				var rootObject = (targetObject as UE.GameObject);

				var articulationBodies = rootObject.GetComponentsInChildren<UE.ArticulationBody>();

				// Due to aritucaltion body transformmation
				foreach (var body in articulationBodies)
				{
					body.enabled = false;
				}

				var baseHelpers = rootObject.GetComponentsInChildren<SDF.Helper.Base>();
				foreach (var baseHelper in baseHelpers)
				{
					var pose = baseHelper?.Pose;
					if (pose == null)
					{
						continue;
					}

					var localPosition = SDF2Unity.Position(pose?.Pos);
					var localRotation = SDF2Unity.Rotation(pose?.Rot);

					var parentObject = baseHelper.transform.parent;

					if (string.IsNullOrEmpty(pose.relative_to))
					{
						// UE.Debug.Log($"{baseHelper.name}: non relative_to baseHelper: {localPosition.ToString("F5")} {localRotation.eulerAngles.ToString("F5")}");
						var rootModelTransform = FindRootParentModel(baseHelper);
						localRotation = UE.Quaternion.Inverse(UE.Quaternion.Inverse(rootModelTransform.localRotation) * parentObject.localRotation) * localRotation;
						localPosition = (localPosition - (parentObject.position - rootModelTransform.position));
					}
					else
					{
						var relativeObjectBaseHelper =
							baseHelper.RootModel.GetComponentsInChildren<SDF.Helper.Base>()
								.FirstOrDefault(x => x.name.Equals(pose.relative_to));

						if (relativeObjectBaseHelper != null)
						{
							var relativeObject = relativeObjectBaseHelper.transform;

							// UE.Debug.Log(linkHelper.name + ":  ImportLink:  => parent " + parentObject.name + ",  " +
							// 	parentObject.localPosition.ToString("F9") + ", " + parentObject.position.ToString("F9"));
							// UE.Debug.Log(linkHelper.name + ":  ImportLink:  => parent " + parentObject.name + ",  " +
							// 	parentObject.localRotation.eulerAngles.ToString("F9") + ", " + parentObject.rotation.eulerAngles.ToString("F9"));

							// UE.Debug.Log(linkHelper.name + ":  ImportLink:  => relative_to " + relativeObject.name + ",  " +
							// 	relativeObject.localPosition.ToString("F9") + ", " + relativeObject.position.ToString("F9"));
							// UE.Debug.Log(linkHelper.name + ":  ImportLink:  => relative_to " + relativeObject.name + ",  " +
							// 	relativeObject.localRotation.eulerAngles.ToString("F9") + ", " + relativeObject.rotation.eulerAngles.ToString("F9"));

							localPosition = localPosition + (relativeObject.position - parentObject.position);
							localRotation = UE.Quaternion.Inverse(UE.Quaternion.Inverse(parentObject.localRotation) * relativeObject.localRotation) * localRotation;
						}
						else
						{
							UE.Debug.LogWarning($"{baseHelper.name}: AdjustPose: relative_to: {pose.relative_to} NOT FOUND !!!!!!");
						}
					}

					baseHelper.SetPose(localPosition, localRotation);
					baseHelper.ResetPose();
				}

				foreach (var body in articulationBodies)
				{
					body.enabled = true;
				}
			}
		}
	}
}