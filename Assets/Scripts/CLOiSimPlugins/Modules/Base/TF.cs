/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class TF
{
	private const int targetPoseFrame = 1;

	public string parentFrameId = string.Empty;
	public string childFrameId = string.Empty;
	public SDF.Helper.Link link = null;
	public TF(in SDF.Helper.Link link, in string childFrameId, in string parentFrameId = "base_link")
	{
		this.parentFrameId = parentFrameId.Replace("::", "_");
		this.childFrameId = childFrameId.Replace("::", "_");
		this.link = link;
	}

	public Pose GetPose()
	{
		var tfLink = this.link;
		var tfPose = tfLink.GetPose(targetPoseFrame);

		if (!tfLink.Model.Equals(tfLink.RootModel))
		{
			var modelPose = tfLink.Model.GetPose(targetPoseFrame);

			modelPose.rotation *= Quaternion.AngleAxis(180, Vector3.up);

			tfPose.position = tfPose.position + modelPose.position;
			tfPose.rotation = tfPose.rotation * modelPose.rotation;
		}

		return tfPose;
	}
}