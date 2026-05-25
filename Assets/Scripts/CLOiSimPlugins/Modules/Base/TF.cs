/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class TF
{
	private string _parentFrameId = string.Empty;
	private string _childFrameId = string.Empty;

	private SDFormat.Helper.Link _link = null;
	private SDFormat.Helper.Base _cachedParentFrameHelper = null;

	public string ParentFrameID => _parentFrameId;
	public string ChildFrameID => _childFrameId;

	public static string NormalizeFrameId(in string frameId)
	{
		return string.IsNullOrEmpty(frameId)
			? string.Empty
			: frameId.Replace("::", "_").TrimStart('/');
	}

	public TF(in SDFormat.Helper.Link link, in string childFrameId, in string parentFrameId)
	{
		_parentFrameId = NormalizeFrameId(parentFrameId);
		_childFrameId = NormalizeFrameId(childFrameId);
		_link = link;
		_cachedParentFrameHelper = ResolveParentFrameHelper(_parentFrameId);
	}

	public Pose GetPose()
	{
		if (_link == null)
			return Pose.identity;

		var childWorldPose = _link.GetWorldPoseSnapshot();

		if (_cachedParentFrameHelper == null)
		{
			return Pose.identity;
		}

		var parentWorldPose = _cachedParentFrameHelper.GetWorldPoseSnapshot();
		return GetRelativePose(parentWorldPose, childWorldPose);
	}

	public static Pose GetRelativePose(Pose parent, Pose child)
	{
		var invParentRot = Quaternion.Inverse(parent.rotation);

		var relativePos = invParentRot * (child.position - parent.position);
		var relativeRot = invParentRot * child.rotation;

		relativeRot = NormalizeQuaternion(relativeRot);

		return new Pose(relativePos, relativeRot);
	}

	private SDFormat.Helper.Base ResolveParentFrameHelper(in string parentFrameId)
	{
		if (string.IsNullOrEmpty(parentFrameId) || _link == null)
			return null;

		var searchRoot = (_link.RootModel != null) ? _link.RootModel.transform : _link.transform.root;

		if (searchRoot == null)
			return null;

		// First, try to resolve the parent frame using the exact prefixed frame id.
		var parentFrameTransform = SafeFindTransformByName(searchRoot, parentFrameId);

		if (parentFrameTransform != null)
		{
			return parentFrameTransform.GetComponent<SDFormat.Helper.Base>();
		}

		// Fallback:
		// If TF frame ids are prefixed but Unity Transform names are local,
		// remove the inferred prefix from the parent frame id.
		//
		// Important:
		// The local-name fallback must be scoped to the current nested model.
		// Searching the entire root can accidentally match the opposite hand
		// because left_hand and right_hand contain duplicated local link names.
		var localParentFrameId = GetLocalParentFrameId(parentFrameId);

		if (!string.IsNullOrEmpty(localParentFrameId) && localParentFrameId != parentFrameId)
		{
			var scopedRoot = ResolveScopedSearchRootForLocalFallback();

			if (scopedRoot != null)
			{
				parentFrameTransform = SafeFindTransformByName(scopedRoot, localParentFrameId);

				if (parentFrameTransform != null)
				{
					return parentFrameTransform.GetComponent<SDFormat.Helper.Base>();
				}
			}

			// Do not fallback to the whole root here.
			// Otherwise, duplicated local names may bind a right-hand child to a left-hand parent, or vice versa.
			return null;
		}

		return null;
	}

	private string GetLocalParentFrameId(in string parentFrameId)
	{
		if (_link == null)
			return parentFrameId;

		var childLocalName = NormalizeFrameId(_link.name);
		var childFrameId = NormalizeFrameId(_childFrameId);
		var normalizedParentFrameId = NormalizeFrameId(parentFrameId);

		if (string.IsNullOrEmpty(childLocalName) ||
			string.IsNullOrEmpty(childFrameId) ||
			string.IsNullOrEmpty(normalizedParentFrameId))
		{
			return normalizedParentFrameId;
		}

		// Infer the prefix only when the child TF frame id ends with the local link name.
		// Example:
		//   childFrameId   = right_hand_index_middle
		//   childLocalName = index_middle
		//   prefix         = right_hand_
		if (!childFrameId.EndsWith(childLocalName))
		{
			return normalizedParentFrameId;
		}

		var prefixLength = childFrameId.Length - childLocalName.Length;

		if (prefixLength <= 0)
		{
			return normalizedParentFrameId;
		}

		var prefix = childFrameId.Substring(0, prefixLength);

		if (string.IsNullOrEmpty(prefix))
		{
			return normalizedParentFrameId;
		}

		// Remove the same inferred prefix from the parent frame id.
		if (normalizedParentFrameId.StartsWith(prefix))
		{
			return normalizedParentFrameId.Substring(prefix.Length);
		}

		return normalizedParentFrameId;
	}

	private Transform ResolveScopedSearchRootForLocalFallback()
	{
		if (_link == null)
			return null;

		var childLocalName = NormalizeFrameId(_link.name);
		var childFrameId = NormalizeFrameId(_childFrameId);

		if (string.IsNullOrEmpty(childLocalName) || string.IsNullOrEmpty(childFrameId))
			return _link.transform;

		if (!childFrameId.EndsWith(childLocalName))
			return _link.transform;

		var prefixLength = childFrameId.Length - childLocalName.Length;

		if (prefixLength <= 0)
			return _link.transform;

		var prefix = childFrameId.Substring(0, prefixLength).TrimEnd('_');

		if (string.IsNullOrEmpty(prefix))
			return _link.transform;

		// Prefer an ancestor whose Transform name matches the inferred model prefix.
		// Example:
		//   childFrameId   = right_hand_index_middle
		//   childLocalName = index_middle
		//   prefix         = right_hand
		var t = _link.transform;

		while (t != null)
		{
			if (NormalizeFrameId(t.name) == prefix)
			{
				return t;
			}

			t = t.parent;
		}

		var searchRoot = (_link.RootModel != null) ? _link.RootModel.transform : _link.transform.root;

		if (searchRoot == null)
			return _link.transform;

		// If the model root is not an ancestor, try to find it under the root model.
		var modelRoot = SafeFindTransformByName(searchRoot, prefix);

		if (modelRoot != null)
			return modelRoot;

		// If the scoped root cannot be resolved, avoid searching the entire root.
		// Returning the current link transform is safer than binding to an unrelated duplicated local name.
		return _link.transform;
	}

	private static Quaternion NormalizeQuaternion(Quaternion q)
	{
		var mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

		if (mag < 1e-8f)
			return Quaternion.identity;

		var inv = 1.0f / mag;

		return new Quaternion(
			q.x * inv,
			q.y * inv,
			q.z * inv,
			q.w * inv);
	}

	private static Transform SafeFindTransformByName(Transform root, string name)
	{
		if (root == null || string.IsNullOrEmpty(name))
			return null;

		if (root.name == name)
			return root;

		for (int i = 0; i < root.childCount; i++)
		{
			var found = SafeFindTransformByName(root.GetChild(i), name);
			if (found != null)
				return found;
		}

		return null;
	}
}