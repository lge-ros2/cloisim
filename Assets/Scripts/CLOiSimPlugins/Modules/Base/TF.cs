/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class TF
{
	private const int TargetPoseFrame = 1;

	public string _parentFrameId = string.Empty;
	public string _childFrameId = string.Empty;
	private SDF.Helper.Link _link = null;

	public string ParentFrameID => _parentFrameId;
	public string ChildFrameID => _childFrameId;


	public TF(in SDF.Helper.Link link, in string childFrameId, in string parentFrameId)
	{
		this._parentFrameId = parentFrameId.Replace("::", "_");
		this._childFrameId = childFrameId.Replace("::", "_");
		this._link = link;
		// Debug.LogFormat("{0} <- {1}", parentFrameId, childFrameId);
	}

	public Pose GetPose()
	{
		var tfPose = Pose.identity;
		var modelPose = _link.Model.GetPose(TargetPoseFrame);
		var linkJointPose = _link.LinkJointPose;
		var linkPoseMoving = _link.GetPose(TargetPoseFrame);

		if (!_link.Model.Equals(_link.RootModel))
		{
			tfPose.position += modelPose.position;
			tfPose.rotation *= modelPose.rotation;
		}

		tfPose.position += linkJointPose.position;
		tfPose.rotation *= linkPoseMoving.rotation;

		return tfPose;
	}
}