/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class TF
{
	public string parentFrameId = string.Empty;
	public string childFrameId = string.Empty;
	public SDF.Helper.Link link = null;
	public TF(in SDF.Helper.Link link, in string childFrameId, in string parentFrameId = "base_link")
	{
		this.parentFrameId = parentFrameId;
		this.childFrameId = childFrameId;
		this.link = link;
	}

	public Pose GetPose()
	{
		var tfLink = this.link;
		var tfPose = tfLink.GetPose();

		if (!tfLink.Model.Equals(tfLink.RootModel))
		{
			tfPose = tfPose.GetTransformedBy(tfLink.Model.GetPose());
		}

		return tfPose;
	}
}