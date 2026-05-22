/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using SDFormat.Implement;
using UnityEngine;

public class TF
{
	public string _parentFrameId = string.Empty;
	public string _childFrameId = string.Empty;
	private SDFormat.Helper.Link _link = null;
	private SDFormat.Helper.Base _cachedParentFrameHelper = null;

	public string ParentFrameID => _parentFrameId;
	public string ChildFrameID => _childFrameId;

	public static string NormalizeFrameId(in string frameId)
	{
		return frameId.Replace("::", "_");
	}

	public TF(in SDFormat.Helper.Link link, in string childFrameId, in string parentFrameId)
	{
		_parentFrameId = NormalizeFrameId(parentFrameId);
		_childFrameId = NormalizeFrameId(childFrameId);
		_link = link;
		_cachedParentFrameHelper = ResolveParentFrameHelper(parentFrameId);
		// Debug.LogFormat("{0} <- {1}", parentFrameId, childFrameId);
	}

	public Pose GetPose()
	{
		var childWorldPose = _link.GetWorldPoseSnapshot();

		if (_cachedParentFrameHelper == null)
		{
			return childWorldPose;
		}

		var parentWorldPose = _cachedParentFrameHelper.GetWorldPoseSnapshot();

		return new Pose(
			Quaternion.Inverse(parentWorldPose.rotation) * (childWorldPose.position - parentWorldPose.position),
			Quaternion.Inverse(parentWorldPose.rotation) * childWorldPose.rotation);
	}

	private SDFormat.Helper.Base ResolveParentFrameHelper(in string parentFrameId)
	{
		if (string.IsNullOrEmpty(parentFrameId) || _link == null)
		{
			return null;
		}

		var searchRoot = (_link.RootModel != null) ? _link.RootModel.transform : _link.transform.root;
		if (searchRoot == null)
		{
			return null;
		}

		var parentFrameTransform = searchRoot.FindTransformByName(parentFrameId);
		return parentFrameTransform?.GetComponent<SDFormat.Helper.Base>();
	}
}