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
	using Implement;

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
					UE.Debug.LogWarning($"SpecifyPoseRelative(): baseHelper: {baseHelper.name} or targetBaseHelper: {targetBaseHelper.name} is null");
					return;
				}

				var parentObject = baseHelper.transform.parent;

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

			private static Helper.Base FindRelativeObjectInNestedModelScope(UE.Transform startTransform, string poseRelativeTo)
			{
				var nestedModel = startTransform?.GetComponentsInParent<Helper.Model>()
					.FirstOrDefault(model => model != null && model.isNested);

				if (nestedModel == null)
				{
					return null;
				}

				return nestedModel.GetComponentsInChildren<Helper.Base>()
					.FirstOrDefault(helper => helper != null && helper.name.Equals(poseRelativeTo));
			}

			private static Helper.Model FindRootModelInScope(UE.Transform startTransform)
			{
				return startTransform?.GetComponentsInParent<Helper.Model>().LastOrDefault();
			}

			public static Helper.Base FindPoseRelativeObject(UE.Transform startTransform, string poseRelativeTo)
			{
				if (startTransform == null || string.IsNullOrEmpty(poseRelativeTo))
				{
					return null;
				}

				if (poseRelativeTo.Contains("::"))
				{
					var scopedRelativeTransform = startTransform.FindTransformByName(poseRelativeTo);
					return scopedRelativeTransform?.GetComponent<Helper.Base>();
				}

				Helper.Base relativeObjectBaseHelper = null;

				var ancestor = startTransform;
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

				relativeObjectBaseHelper ??= FindRelativeObjectInNestedModelScope(startTransform, poseRelativeTo);
				relativeObjectBaseHelper ??= FindRootModelInScope(startTransform)?.GetComponentsInChildren<Helper.Base>()
					.FirstOrDefault(helper => helper != null && helper.name.Equals(poseRelativeTo));

				return relativeObjectBaseHelper;
			}

			private static UE.ArticulationBody FindParentArticulationBody(UE.ArticulationBody body)
			{
				return body.transform.parent?.GetComponentInParent<UE.ArticulationBody>();
			}

			private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

			private static bool IsFinite(UE.Vector3 v) => IsFinite(v.x) && IsFinite(v.y) && IsFinite(v.z);

			private static bool IsFinite(UE.Quaternion q) =>
				IsFinite(q.x) && IsFinite(q.y) && IsFinite(q.z) && IsFinite(q.w);

			/// <summary>
			/// Validate that an ArticulationBody's world/local transform and inertia are
			/// finite before enabling it. Enabling an AB whose pose contains NaN/Inf builds
			/// a degenerate PhysX articulation that SIGSEGVs natively inside set_enabled
			/// (uncatchable by managed try/catch). Returns false so the caller can skip the
			/// enable and keep the rest of the model alive instead of crashing the process.
			/// </summary>
			private static bool IsArticulationTransformValid(UE.ArticulationBody body)
			{
				var t = body.transform;
				return IsFinite(t.position) && IsFinite(t.rotation) &&
					IsFinite(t.localPosition) && IsFinite(t.localRotation) &&
					IsFinite(t.lossyScale) &&
					IsFinite(body.anchorPosition) && IsFinite(body.anchorRotation) &&
					IsFinite(body.parentAnchorPosition) && IsFinite(body.parentAnchorRotation) &&
					(body.automaticInertiaTensor || IsFinite(body.inertiaTensor));
			}

			private static void UpdateParentAnchor(UE.ArticulationBody body)
			{
				var parentBody = FindParentArticulationBody(body);
				if (parentBody == null)
				{
					return;
				}

				var anchorWorldPosition = body.transform.TransformPoint(body.anchorPosition);
				var anchorWorldRotation = body.transform.rotation * body.anchorRotation;

				body.matchAnchors = false;
				body.parentAnchorPosition = parentBody.transform.InverseTransformPoint(anchorWorldPosition);
				body.parentAnchorRotation = UE.Quaternion.Inverse(parentBody.transform.rotation) * anchorWorldRotation;
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
						// Keep create-time parent selection and pose-time relative_to resolution
						// on the same search rules to avoid frame mismatches for nested models.
						var relativeObjectBaseHelper = FindPoseRelativeObject(baseHelper.transform.parent, poseRelativeTo);

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

				foreach (var body in articulationBodies)
				{
					UpdateParentAnchor(body);
				}

				// Before enabling, mark root ABs in static models as immovable
				// to prevent joint spring forces from shifting them.
				foreach (var body in articulationBodies)
				{
					// A root AB has no parent AB in the hierarchy
					var parentAB = FindParentArticulationBody(body);
					if (parentAB == null)
					{
						var modelHelper = body.GetComponentInParent<Helper.Model>();
						if (modelHelper != null && modelHelper.isStatic)
						{
							body.immovable = true;
						}
					}
				}

				// Optional breadcrumb: the last line logged before a native set_enabled
				// SIGSEGV identifies the offending body. Off by default (one line per body
				// per import); enable with CLOISIM_AB_DEBUG=1 if the crash recurs.
				var abDebug = Environment.GetEnvironmentVariable("CLOISIM_AB_DEBUG") == "1";

				foreach (var body in articulationBodies)
				{
					// A NaN/Inf pose/inertia makes the native PhysX articulation build
					// SIGSEGV inside set_enabled — which managed try/catch cannot trap.
					// Skip the enable for that body (logging it) to keep the process alive.
					if (!IsArticulationTransformValid(body))
					{
						UE.Debug.LogError(
							$"[SpecifyPose] Skipping enable of '{body.name}': non-finite transform/inertia " +
							$"(pos={body.transform.position}, rot={body.transform.rotation.eulerAngles}, " +
							$"anchor={body.anchorPosition}, parentAnchor={body.parentAnchorPosition}). " +
							"This would crash the PhysX articulation build.");
						continue;
					}

					if (abDebug)
					{
						UE.Debug.Log($"[SpecifyPose] enabling AB '{body.name}' isRoot={body.isRoot} pos={body.transform.position:F4}");
					}

					body.enabled = true;

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