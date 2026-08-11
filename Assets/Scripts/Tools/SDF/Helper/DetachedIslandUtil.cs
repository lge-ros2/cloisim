/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Helper
	{
		// ArticulationBody islands split off by SDF import (see KinematicIslandFollower)
		// are reparented out from under their original model into a
		// "{RootModel}_DetachedIslands" container at the World root, so a plain
		// GetComponentsInChildren() call scoped to a robot's own root Transform no
		// longer finds them. Plugins that enumerate their own model's links/joints
		// (TF publishing, joint-state lookup, robot_description generation, etc.)
		// should use this instead of calling GetComponentsInChildren() directly.
		public static class DetachedIslandUtil
		{
			// Maps a model root Transform (e.g. a nested hand model) to the detached
			// subtree roots that were split out of it. Keyed by model root rather than
			// by container name because a nested model's links live in the top-level
			// model's "{TopModel}_DetachedIslands" container (not
			// "{model}_DetachedIslands"), and multiple nested models (left/right hand)
			// share that container with duplicated local joint names. Scoping by the
			// owning model root keeps left/right lookups unambiguous.
			private static readonly Dictionary<UE.Transform, List<UE.Transform>> _ownedDetachedSubtrees = new();

			public static void ClearDetachedSubtrees()
			{
				_ownedDetachedSubtrees.Clear();
			}

			public static void RegisterDetachedSubtree(UE.Transform ownerModelRoot, UE.Transform detachedRoot)
			{
				if (ownerModelRoot == null || detachedRoot == null)
				{
					return;
				}

				if (!_ownedDetachedSubtrees.TryGetValue(ownerModelRoot, out var subtrees))
				{
					subtrees = new List<UE.Transform>();
					_ownedDetachedSubtrees[ownerModelRoot] = subtrees;
				}

				if (!subtrees.Contains(detachedRoot))
				{
					subtrees.Add(detachedRoot);
				}
			}

			// Enumerates components in the given owner model root plus the detached
			// island subtrees that were split out of that same model root.
			public static T[] GetComponentsInChildrenIncludingOwnedDetachedIslands<T>(UE.Transform ownerRoot) where T : UE.Component
			{
				var results = new List<T>(ownerRoot.GetComponentsInChildren<T>());

				if (_ownedDetachedSubtrees.TryGetValue(ownerRoot, out var subtrees))
				{
					foreach (var subtree in subtrees)
					{
						if (subtree != null)
						{
							results.AddRange(subtree.GetComponentsInChildren<T>());
						}
					}
				}

				return results.ToArray();
			}

			// For a top-level model plugin (e.g. Micom attached to the robot root),
			// includes the whole "{TopModel}_DetachedIslands" container. Nested-model
			// plugins must use GetComponentsInChildrenIncludingOwnedDetachedIslands
			// instead to stay scoped to their own subtree.
			public static T[] GetComponentsInChildrenIncludingDetachedIslands<T>(UE.Transform root) where T : UE.Component
			{
				var results = new List<T>(root.GetComponentsInChildren<T>());

				var detachedContainer = FindDetachedContainer(root);
				if (detachedContainer != null)
				{
					results.AddRange(detachedContainer.GetComponentsInChildren<T>());
				}

				return results.ToArray();
			}

			private static UE.Transform FindDetachedContainer(UE.Transform root)
			{
				var topModelName = GetTopModelName(root);
				if (string.IsNullOrEmpty(topModelName))
				{
					return null;
				}

				return Main.WorldRoot.transform.Find($"{topModelName}_DetachedIslands");
			}

			private static string GetTopModelName(UE.Transform root)
			{
				if (root == null)
				{
					return null;
				}

				var model = root.GetComponentInParent<Helper.Model>();
				return model != null ? model.name : root.name;
			}
		}
	}
}
