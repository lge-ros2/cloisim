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
/// SDF plugin that sets up ClothGrabber components on robot fingertip collision links.
///
/// SDF example:
/// <plugin name="ClothGrabberPlugin" filename='libClothGrabberPlugin.so'>
///   <grab_radius>0.02</grab_radius>
///   <grippers>
///     <gripper>
///       <link>robot::gripper_left_finger</link>
///       <collision>gripper_left_finger_collision</collision>
///     </gripper>
///     <gripper>
///       <link>robot::gripper_right_finger</link>
///       <collision>gripper_right_finger_collision</collision>
///     </gripper>
///   </grippers>
/// </plugin>
/// </summary>
public class ClothGrabberPlugin : CLOiSimPlugin
{
	private readonly List<ClothGrabber> _grabbers = new();

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

		var grabRadius = pluginParams.GetValue<float>("grab_radius", 0.01f);

		// Get all <gripper> child elements directly — avoids XPath positional predicate issues
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

		Debug.Log($"ClothGrabberPlugin: Found {gripperElements.Count} gripper(s), grab_radius={grabRadius}");

		for (var i = 0; i < gripperElements.Count; i++)
		{
			var gripperElem = gripperElements[i];
			var gripperIndex = i + 1;

			var linkName = Extensions.FindElementByPath(gripperElem, "link")?.Value?.GetAsString()?.Trim();
			if (string.IsNullOrWhiteSpace(linkName)) continue;

			var collisionName = Extensions.FindElementByPath(gripperElem, "collision")?.Value?.GetAsString()?.Trim();

			var linkTransform = ResolveTargetTransform(linkName);
			if (linkTransform == null)
			{
				Debug.LogWarning($"ClothGrabberPlugin: Cannot find link '{linkName}'");
				continue;
			}

			var meshCollider = FindCollisionMeshCollider(linkTransform, collisionName);
			if (meshCollider == null)
			{
				Debug.LogWarning($"ClothGrabberPlugin: No MeshCollider found on '{linkName}'" +
					(collisionName != null ? $" with collision name '{collisionName}'" : ""));
				continue;
			}

			var grabber = linkTransform.gameObject.AddComponent<ClothGrabber>();
			grabber.GrabCollider = meshCollider;
			grabber.GrabRadius = grabRadius;

			_grabbers.Add(grabber);

			StartSummary.AppendLine($"ClothGrabber[{gripperIndex}]: link='{linkName}', collider='{meshCollider.gameObject.name}', radius={grabRadius}");
		}

		if (_grabbers.Count == 0)
			Debug.LogWarning("ClothGrabberPlugin: No grabbers set up. Check <grippers> in SDF.");

		yield return null;
	}

	protected override void OnReset()
	{
		foreach (var grabber in _grabbers)
		{
			if (grabber != null)
				grabber.Release();
		}
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
