/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CLOiSim.Cloth;
using SDFormat;

/// <summary>
/// SDF plugin that sets up ClothGrabber components on robot fingertip collision links
/// and activates group grabs when all grippers in a group are within contact distance of cloth.
///
/// Activation logic: a group activates (all its grabbers try to grab) when every gripper in the
/// group has a cloth vertex within contact_distance_threshold. A grabber that is already
/// holding a vertex counts as "in contact" so the grab persists. If any grabber in the group
/// loses contact the entire group is deactivated (all grabbers release).
///
/// SDF example:
/// <plugin name="ClothGrabberPlugin" filename='libClothGrabberPlugin.so'>
///   <grab_radius>0.005</grab_radius>
///   <grippers>
///     <gripper name="thumb">
///       <link>thumb_distal</link>
///       <collision>thumb_distal_collision</collision>
///     </gripper>
///     <gripper name="index">
///       <link>index_distal</link>
///       <collision>index_distal_collision</collision>
///     </gripper>
/// 	<gripper name="middle">
/// 	  <link>middle_distal</link>
/// 	  <collision>middle_distal_collision</collision>
/// 	</gripper>
///   </grippers>
///   <activation>
/// 	<group contact_distance_threshold="0.005">
/// 		<gripper>thumb</gripper>
/// 		<gripper>index</gripper>
/// 	</group>
/// 	<group contact_distance_threshold="0.005">
/// 		<gripper>thumb</gripper>
/// 		<gripper>middle</gripper>
/// 	</group>
/// 	<group contact_distance_threshold="0.005">
/// 		<gripper>thumb</gripper>
/// 		<gripper>index</gripper>
/// 		<gripper>middle</gripper>
/// 	</group>
///   </activation>
/// </plugin>
/// </summary>
public class ClothGrabberPlugin : CLOiSimPlugin
{
	private class ActivationGroup
	{
		public float ContactDistanceThreshold;
		public List<ClothGrabber> Grabbers = new();
	}

	private readonly Dictionary<string, ClothGrabber> _grabbersByName = new();
	private readonly List<ActivationGroup> _activationGroups = new();

	// Per-frame cache to avoid redundant HasClothWithinDistance() calls
	// when the same grabber appears in multiple groups
	private readonly Dictionary<ClothGrabber, bool> _contactCache = new();

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.NONE;
	}

	protected override IEnumerator OnStart()
	{
		var pluginParams = GetPluginParameters();
		if (pluginParams == null)
		{
			Debug.LogError("ClothGrabberPlugin: Plugin parameters not found");
			yield break;
		}

		var grabRadius = pluginParams.GetValue("grab_radius", 0.01f);

		// 1. Parse <grippers>
		var grippersElem = pluginParams.GetElement("grippers");
		if (grippersElem == null)
		{
			Debug.LogWarning("ClothGrabberPlugin: No <grippers> element found");
			yield break;
		}

		var gripperElements = grippersElem.GetElements("gripper");
		if (gripperElements.Count == 0)
		{
			Debug.LogWarning("ClothGrabberPlugin: No <gripper> entries found under <grippers>");
			yield break;
		}

		foreach (var gripperElem in gripperElements)
		{
			var gripperName = gripperElem.GetAttribute<string>("name")?.Trim();
			if (string.IsNullOrWhiteSpace(gripperName)) continue;

			var linkName = gripperElem.FindElementByPath("link")?.Value?.GetAsString()?.Trim();
			if (string.IsNullOrWhiteSpace(linkName)) continue;

			var collisionName = gripperElem.FindElementByPath("collision")?.Value?.GetAsString()?.Trim();

			var linkTransform = ResolveTargetTransform(linkName);
			if (linkTransform == null)
			{
				Debug.LogWarning($"ClothGrabberPlugin: Cannot find link \'{linkName}\' for gripper \'{gripperName}\'");
				continue;
			}

			var meshCollider = FindCollisionMeshCollider(linkTransform, collisionName);
			if (meshCollider == null)
			{
				Debug.LogWarning($"ClothGrabberPlugin: No MeshCollider found on \'{linkName}\'" +
					(collisionName != null ? $" (collision=\'{collisionName}\')" : ""));
				continue;
			}

			var grabber = linkTransform.gameObject.AddComponent<ClothGrabber>();
			grabber.GrabCollider = meshCollider;
			grabber.GrabRadius = grabRadius;

			_grabbersByName[gripperName] = grabber;

			StartSummary.AppendLine(
				$"Gripper \'{gripperName}\': link=\'{linkName}\', collider=\'{meshCollider.gameObject.name}\', radius={grabRadius}");
		}

		if (_grabbersByName.Count == 0)
		{
			Debug.LogWarning("ClothGrabberPlugin: No grabbers set up. Check <grippers> in SDF.");
			yield return null;
			yield break;
		}

		// 2. Parse <activation> groups
		var activationElem = pluginParams.GetElement("activation");
		if (activationElem == null)
		{
			Debug.LogWarning("ClothGrabberPlugin: No <activation> element -- grabbers will never activate.");
			yield return null;
			yield break;
		}

		var groupElements = activationElem.GetElements("group");
		foreach (var groupElem in groupElements)
		{
			var threshold = groupElem.GetAttribute("contact_distance_threshold", 0.01f);
			var group = new ActivationGroup { ContactDistanceThreshold = threshold };

			var groupGripperElems = groupElem.GetElements("gripper");
			var resolvedNames = new List<string>();
			foreach (var nameElem in groupGripperElems)
			{
				var name = nameElem.Value?.GetAsString()?.Trim();
				if (string.IsNullOrWhiteSpace(name)) continue;
				if (_grabbersByName.TryGetValue(name, out var grabber))
				{
					group.Grabbers.Add(grabber);
					resolvedNames.Add(name);
				}
				else
				{
					Debug.LogWarning($"ClothGrabberPlugin: Group references unknown gripper \'{name}\'");
				}
			}

			if (group.Grabbers.Count > 0)
			{
				_activationGroups.Add(group);
				StartSummary.AppendLine(
					$"ActivationGroup: grippers=[{string.Join(", ", resolvedNames)}], contact_distance_threshold={threshold}");
			}
		}

		if (_activationGroups.Count == 0)
			Debug.LogWarning("ClothGrabberPlugin: No activation groups configured.");

		yield return null;
	}

	protected override void OnReset()
	{
		foreach (var grabber in _grabbersByName.Values)
		{
			if (grabber != null)
				grabber.Deactivate();
		}
	}

	private void Update()
	{
		_contactCache.Clear();

		foreach (var group in _activationGroups)
		{
			var allInContact = true;
			foreach (var grabber in group.Grabbers)
			{
				if (!IsInContact(grabber, group.ContactDistanceThreshold))
				{
					allInContact = false;
					break;
				}
			}

			if (allInContact)
			{
				foreach (var grabber in group.Grabbers)
				{
					if (!grabber.IsActive)
						grabber.Activate();
				}
			}
			else
			{
				foreach (var grabber in group.Grabbers)
				{
					if (grabber.IsActive)
						grabber.Deactivate();
				}
			}
		}
	}

	/// <summary>
	/// A grabber is "in contact" when it is already holding a vertex (distance is effectively zero)
	/// or when a free cloth vertex is within <paramref name="threshold"/>.
	/// </summary>
	private bool IsInContact(ClothGrabber grabber, float threshold)
	{
		if (grabber == null) return false;

		if (_contactCache.TryGetValue(grabber, out var cached))
			return cached;

		var result = grabber.IsGrabbing || grabber.HasClothWithinDistance(threshold);
		_contactCache[grabber] = result;
		return result;
	}

	/// <summary>
	/// Finds a MeshCollider under the given link transform.
	/// If collisionName is specified, searches for a child with that name first.
	/// </summary>
	private static MeshCollider FindCollisionMeshCollider(Transform linkTransform, string collisionName)
	{
		if (!string.IsNullOrEmpty(collisionName))
		{
			var target = FindInHierarchy(linkTransform, collisionName);
			if (target != null)
			{
				var mc = target.GetComponent<MeshCollider>();
				if (mc != null) return mc;
			}
		}

		// Fallback: first MeshCollider anywhere under link (non-trigger preferred)
		var allColliders = linkTransform.GetComponentsInChildren<MeshCollider>(includeInactive: false);
		foreach (var mc in allColliders)
		{
			if (!mc.isTrigger) return mc;
		}
		// If only triggers exist, return the first one
		if (allColliders.Length > 0) return allColliders[0];

		return null;
	}

	private Transform ResolveTargetTransform(string target)
	{
		if (string.IsNullOrEmpty(target)) return null;
		(_, var linkName) = SDF2Unity.GetModelLinkName(target);
		return FindInHierarchy(transform, linkName);
	}

	private static Transform FindInHierarchy(Transform root, string name)
	{
		if (root.name == name) return root;
		foreach (Transform child in root)
		{
			var result = FindInHierarchy(child, name);
			if (result != null) return result;
		}
		return null;
	}
}
