/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Linq;
using System;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public static class Util
		{
			public static void SetChild(this UE.GameObject parent, in UE.GameObject child)
			{
				child.transform.position = UE.Vector3.zero;
				child.transform.rotation = UE.Quaternion.identity;

				var targetParentTransform = (parent == null) ? Main.WorldRoot.transform : parent.transform;
				child.transform.SetParent(targetParentTransform, false);

				child.transform.localScale = UE.Vector3.one;
				child.transform.localPosition = UE.Vector3.zero;
				child.transform.localRotation = UE.Quaternion.identity;
			}

			private static UE.Transform FindRootParentModel(Helper.Base targetBaseHelper)
			{
				if (targetBaseHelper == null)
					return null;

				var foundRootModelTransform = targetBaseHelper.RootModel.transform;

				UE.Transform FindParent<T>(UE.Transform start, Func<T, bool> condition) where T : UE.Component
				{
					for (var parent = start.parent; parent != null; parent = parent.parent)
					{
						var comp = parent.GetComponent<T>();
						if (comp != null && condition(comp))
							return comp.transform;
					}
					return null;
				}

				switch (targetBaseHelper)
				{
					case Helper.Model modelHelper:
						{
							var result = FindParent<Helper.Model>(
								modelHelper.transform,
								m => !m.isNested
							);
							if (result != null)
								foundRootModelTransform = result;
							break;
						}

					case Helper.Link linkHelper:
						{
							var result = FindParent<Helper.Link>(
								linkHelper.transform,
								l => l.Model.isNested
							);
							if (result != null)
								foundRootModelTransform = result;
							break;
						}

					default:
						{
							var result = FindParent<Helper.Link>(
								targetBaseHelper.transform,
								_ => true
							);
							if (result != null)
								foundRootModelTransform = result;
							break;
						}
				}

				return foundRootModelTransform;
			}

			private static void SpecifyPoseAbsolute(in Helper.Base baseHelper, ref UE.Vector3 localPosition, ref UE.Quaternion localRotation)
			{
				var parentObject = baseHelper.transform.parent;

				var rootModelTransform = FindRootParentModel(baseHelper);
				// UE.Debug.Log($"SpecifyPose {baseHelper.name}: non relative_to baseHelper: {localRotation.eulerAngles.ToString("F5")} rootModelTransform: {rootModelTransform.name}");
				// UE.Debug.LogWarning($"SpecifyPose {baseHelper.name}: {rootModelTransform.localRotation.eulerAngles.ToString("F6")} * {parentObject.localRotation.eulerAngles.ToString("F6")} * {localRotation.eulerAngles.ToString("F6")}");
				// UE.Debug.LogWarning($"SpecifyPose {baseHelper.name}: rootModelTransform == {rootModelTransform.name} <-> {parentObject.name}");

				// For elements directly inside a nested model, poses are relative
				// to that model — no root model offset adjustment needed.
				var parentModelHelper = parentObject?.GetComponent<Helper.Model>();
				if (parentModelHelper != null && parentModelHelper.isNested)
				{
					rootModelTransform = parentObject;
				}

				var rotationOffset = rootModelTransform.Equals(parentObject) ? UE.Quaternion.identity : parentObject.localRotation;
				var positionOffset = rootModelTransform.Equals(parentObject) ? UE.Vector3.zero : (parentObject.position - rootModelTransform.position);
				positionOffset = UE.Quaternion.Inverse(rootModelTransform.localRotation) * positionOffset;

				localPosition = localPosition - positionOffset;
				localRotation = rotationOffset * localRotation;
			}

			private static void SpecifyPoseRelative(in Helper.Base baseHelper, in Helper.Base targetBaseHelper, ref UE.Vector3 localPosition, ref UE.Quaternion localRotation)
			{
				if (baseHelper == null || targetBaseHelper == null)
				{
					UE.Debug.LogWarning($"SpecifyPoseRelative(): baseHelper: {baseHelper.name} or targgetBaseHelper: {targetBaseHelper.name} is null");
					return;
				}

				var parentObject = baseHelper.transform.parent;
				var pose = baseHelper?.Pose;

				var relativeObject = targetBaseHelper.transform;

				// UE.Debug.Log($"SpecifyPose {relativeObject.name}: ImportLink: => parent {parentObject.name}, {parentObject.localPosition.ToString("F9")}, {parentObject.position.ToString("F9")}");
				// UE.Debug.Log($"SpecifyPose {relativeObject.name}: ImportLink: => parent {parentObject.name}, {parentObject.localRotation.eulerAngles.ToString("F9")}, {parentObject.rotation.eulerAngles.ToString("F9")}");
				// UE.Debug.Log($"SpecifyPose {relativeObject.name}: ImportLink: => relative_to {relativeObject.name}, {relativeObject.localPosition.ToString("F9")}, {relativeObject.position.ToString("F9")}");
				// UE.Debug.Log($"SpecifyPose {relativeObject.name}: ImportLink: => relative_to {relativeObject.name}, {relativeObject.localRotation.eulerAngles.ToString("F9")}, {relativeObject.rotation.eulerAngles.ToString("F9")}");

				if (!relativeObject.Equals(parentObject))
				{
					// localPosition is expressed in relativeObject's local frame.
					// Transform it into parentObject's local frame:
					//   worldPos = relativeObject.rotation * localPosition + relativeObject.position
					//   localPos_in_parent = Inverse(parentObject.rotation) * (worldPos - parentObject.position)
					var worldPos = relativeObject.rotation * localPosition + relativeObject.position;
					localPosition = UE.Quaternion.Inverse(parentObject.rotation) * (worldPos - parentObject.position);
					localRotation = UE.Quaternion.Inverse(parentObject.rotation) * relativeObject.rotation * localRotation;
					UE.Debug.LogWarning($"SpecifyPoseRelative(): relativeObject: {relativeObject.name} is not the same as parentObject: {parentObject.name}, applying relative pose transformation");
				}
			}

			public static void SpecifyPose(this object targetObject)
			{
				var rootObject = targetObject as UE.GameObject;

				var articulationBodies = rootObject.GetComponentsInChildren<UE.ArticulationBody>();

				// Due to articulation body transformation
				foreach (var body in articulationBodies)
				{
					body.enabled = false;
				}

				foreach (var baseHelper in rootObject.GetComponentsInChildren<Helper.Base>())
				{
					var pose = baseHelper?.Pose;
					if (pose == null)
					{
						continue;
					}

					var (localPosition, localRotation) = pose.Value.ToUnity();

					var poseRelativeTo = baseHelper?.PoseRelativeTo;
					if (string.IsNullOrEmpty(poseRelativeTo))
					{
						SpecifyPoseAbsolute(baseHelper, ref localPosition, ref localRotation);
					}
					else
					{
						Helper.Base relativeObjectBaseHelper = null;

						// First: check direct parent — after joint re-parenting, pose relative_to
						// almost always refers to the direct parent link. Matching by name here avoids
						// cross-model collisions (e.g., left_hand and right_hand both having
						// "hand_base_link") that arise when searching from the root.
						var directParent = baseHelper.transform.parent;
						if (directParent != null && directParent.name.Equals(poseRelativeTo))
						{
							relativeObjectBaseHelper = directParent.GetComponent<Helper.Base>();
						}

						// Second: search ancestors and their immediate children for named frames.
						// This covers cases like collision/visual poses relative to a sibling or
						// parent that isn't the immediate parent.
						if (relativeObjectBaseHelper == null)
						{
							var ancestor = directParent;
							while (ancestor != null && relativeObjectBaseHelper == null)
							{
								if (ancestor.name.Equals(poseRelativeTo))
								{
									relativeObjectBaseHelper = ancestor.GetComponent<Helper.Base>();
								}
								else
								{
									var found = ancestor.Find(poseRelativeTo);
									if (found != null)
									{
										relativeObjectBaseHelper = found.GetComponent<Helper.Base>();
									}
								}
								ancestor = ancestor.parent;
							}
						}

						// Fallback: root model scope for cross-model references
						relativeObjectBaseHelper ??= baseHelper.RootModel?.GetComponentsInChildren<Helper.Base>()
							.FirstOrDefault(x => x.name.Equals(poseRelativeTo));

						if (relativeObjectBaseHelper != null)
						{
							SpecifyPoseRelative(baseHelper, relativeObjectBaseHelper, ref localPosition, ref localRotation);
						}
						else
						{
							UE.Debug.LogWarning($"{baseHelper.name}: AdjustPose: relative_to: {poseRelativeTo} NOT FOUND !!!!!!");
						}
					}

					baseHelper.SetPose(localPosition, localRotation);
					baseHelper.ResetPose();
				}

				// Before enabling, mark root ABs in static models as immovable
				// to prevent joint spring forces from shifting them.
				foreach (var body in articulationBodies)
				{
					// A root AB has no parent AB in the hierarchy
					var parentAB = body.transform.parent?.GetComponentInParent<UE.ArticulationBody>();
					if (parentAB == null)
					{
						var modelHelper = body.GetComponentInParent<Helper.Model>();
						if (modelHelper != null && modelHelper.isStatic)
						{
							body.immovable = true;
						}
					}
				}

				foreach (var body in articulationBodies)
				{
					body.enabled = true;

					// Adjust manual center of mass by anchor offset for non-root bodies.
					// SDF defines inertial pose relative to the link frame, but Unity's
					// ArticulationBody local origin may differ from the link frame by the
					// anchor offset after joint re-parenting.
					if (!body.isRoot && !body.automaticCenterOfMass)
					{
						var oldCenterOfMass = body.centerOfMass;
						body.centerOfMass = oldCenterOfMass + body.anchorPosition;
#if UNITY_EDITOR
						UE.Debug.LogWarning($"Adjusting center of mass for {body.name} by anchor offset {body.anchorPosition.ToString("F5")} due to non-root articulation body with manual center of mass.  Old center of mass: {oldCenterOfMass.ToString("F5")}, New center of mass: {body.centerOfMass.ToString("F5")}");
#endif
					}

					if (body.isRoot)
					{
						body.TeleportRoot(body.transform.position, body.transform.rotation);
						body.Sleep();
					}
				}

				var devices = rootObject.GetComponentsInChildren<Device>();
				foreach (var device in devices)
				{
					device.UpdatePose();
					// UE.Debug.LogWarning($"{device.DeviceName} {device.GetPose()}");
				}
			}
		}
	}
}