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
				if (targetBaseHelper == null)
					return null;

				var foundRootModelTransform = targetBaseHelper.RootModel.transform;

				// 공통 부모 탐색 함수
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
					case SDF.Helper.Model modelHelper:
						{
							var result = FindParent<SDF.Helper.Model>(
								modelHelper.transform,
								m => !m.isNested
							);
							if (result != null)
								foundRootModelTransform = result;
							break;
						}

					case SDF.Helper.Link linkHelper:
						{
							var result = FindParent<SDF.Helper.Link>(
								linkHelper.transform,
								l => l.Model.isNested
							);
							if (result != null)
								foundRootModelTransform = result;
							break;
						}

					default:
						{
							var result = FindParent<SDF.Helper.Link>(
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

			private static void SpecifyPoseAbsolute(in SDF.Helper.Base baseHelper, ref UE.Vector3 localPosition, ref UE.Quaternion localRotation)
			{
				var parentObject = baseHelper.transform.parent;

				var rootModelTransform = FindRootParentModel(baseHelper);
				// UE.Debug.Log($"SpecifyPose {baseHelper.name}: non relative_to baseHelper: {localRotation.eulerAngles.ToString("F5")} rootModelTransform: {rootModelTransform.name}");
				// UE.Debug.LogWarning($"SpecifyPose {baseHelper.name}: {rootModelTransform.localRotation.eulerAngles.ToString("F6")} * {parentObject.localRotation.eulerAngles.ToString("F6")} * {localRotation.eulerAngles.ToString("F6")}");
				// UE.Debug.LogWarning($"SpecifyPose {baseHelper.name}: rootModelTransform == {rootModelTransform.name} <-> {parentObject.name}");

				var rotationOffset = (rootModelTransform.Equals(parentObject)) ? UE.Quaternion.identity : parentObject.localRotation;
				var positionOffset = (rootModelTransform.Equals(parentObject)) ? UE.Vector3.zero : (parentObject.position - rootModelTransform.position);
				positionOffset = UE.Quaternion.Inverse(rootModelTransform.localRotation) * positionOffset;

				localPosition = localPosition - positionOffset;
				localRotation = rotationOffset * localRotation;
			}

			private static void SpecifyPoseRelative(in SDF.Helper.Base baseHelper, in SDF.Helper.Base targetBaseHelper, ref UE.Vector3 localPosition, ref UE.Quaternion localRotation)
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

				var positionOffset = (relativeObject.Equals(parentObject)) ? UE.Vector3.zero : (relativeObject.position - parentObject.position);
				var rotationOffset = (relativeObject.Equals(parentObject)) ? UE.Quaternion.identity : (parentObject.localRotation * relativeObject.localRotation);

				localPosition = localPosition + positionOffset;
				localRotation = rotationOffset * localRotation;
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

				foreach (var baseHelper in rootObject.GetComponentsInChildren<SDF.Helper.Base>())
				{
					var pose = baseHelper?.Pose;
					if (pose != null)
					{
						var localPosition = pose?.Pos.ToUnity() ?? UE.Vector3.zero;
						var localRotation = pose?.Rot.ToUnity() ?? UE.Quaternion.identity;

						// UE.Debug.Log($"SpecifyPose {baseHelper.name} {pose.relative_to}");
						if (string.IsNullOrEmpty(pose.relative_to))
						{
							SpecifyPoseAbsolute(baseHelper, ref localPosition, ref localRotation);
						}
						else
						{
							var relativeObjectBaseHelper
								= baseHelper.RootModel.GetComponentsInChildren<SDF.Helper.Base>().FirstOrDefault(x => x.name.Equals(pose.relative_to));

							if (relativeObjectBaseHelper != null)
							{
								SpecifyPoseRelative(baseHelper, relativeObjectBaseHelper, ref localPosition, ref localRotation);
							}
							else
							{
								UE.Debug.LogWarning($"{baseHelper.name}: AdjustPose: relative_to: {pose.relative_to} NOT FOUND !!!!!!");
							}

						}

						baseHelper.SetPose(localPosition, localRotation);
						baseHelper.ResetPose();
					}
				}

				foreach (var body in articulationBodies)
				{
					body.enabled = true;
				}
			}
		}
	}
}