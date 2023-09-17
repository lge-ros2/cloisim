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

	public TF(in SDF.Helper.Link link)
	{
		var parentFrame = link.JointParentLinkName;
		var childFrame = link.JointChildLinkName;
		this.parentFrameId = parentFrame.Replace("::", "_");
		this.childFrameId = childFrame.Replace("::", "_");
		this.link = link;
	}

	public Pose GetPose()
	{
		var tfLink = this.link;
		var tfPose = tfLink.GetPose(targetPoseFrame);

		if (!tfLink.Model.Equals(tfLink.RootModel))
		{
			var modelPose = tfLink.Model.GetPose(targetPoseFrame);

			// Debug.Log(parentFrameId + "::" + childFrameId + "(" + tfLink.JointAxis + ") = " + modelPose.position +", " + tfPose.position);
			if (tfLink.JointAxis.Equals(Vector3.up) || tfLink.JointAxis.Equals(Vector3.down))
			{
				tfPose.rotation *= Quaternion.AngleAxis(180, Vector3.right);
				Debug.Log(parentFrameId + "::" + childFrameId + "(" + tfLink.JointAxis + ") = " + modelPose.position + ", " + tfPose.position);
			}
			else if (tfLink.JointAxis.Equals(Vector3.forward) || tfLink.JointAxis.Equals(Vector3.back))
			{
				// tfPose.rotation *= Quaternion.AngleAxis(180, Vector3.up);
				Debug.Log(parentFrameId + "::" + childFrameId + "(" + tfLink.JointAxis + ") = " + modelPose.position + ", " + tfPose.position);
			}
			else if (tfLink.JointAxis.Equals(Vector3.left) || tfLink.JointAxis.Equals(Vector3.right))
			{
				// tfPose.rotation *= Quaternion.AngleAxis(180, Vector3.forward);
				Debug.Log(parentFrameId + "::" + childFrameId + "(" + tfLink.JointAxis + ") = " + modelPose.position + ", " + tfPose.position);
			}

			tfPose.position += modelPose.position;
			tfPose.rotation *= modelPose.rotation;
		}

		return tfPose;
	}
}