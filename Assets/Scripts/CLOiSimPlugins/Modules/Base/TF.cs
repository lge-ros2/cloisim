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
		// Debug.LogFormat("{0} <- {1}", parentFrameId, childFrameId);
	}

	public Pose GetPose()
	{
		var tfLink = this.link;
		var tfPose = tfLink.GetPose(targetPoseFrame);
		var modelPose = tfLink.Model.GetPose(targetPoseFrame);

		// Debug.Log(parentFrameId + " <= " + childFrameId + "(" + tfLink.JointAxis + ")" + tfPose);
		if (!tfLink.Model.Equals(tfLink.RootModel))
		{
			tfPose.rotation = modelPose.rotation * tfPose.rotation;

			if (tfLink.Model != null)
			{
				tfPose.position += modelPose.position;
			}
		}
		else
		{
			tfPose.rotation *= modelPose.rotation;
			// Debug.Log("is Root Model TF");
		}
		// Debug.Log(parentFrameId + "::" + childFrameI + "(" + tfLink.JointAxis + ") = tf rot" + tfPose.rotation.eulerAngles);
		return tfPose;
	}
}