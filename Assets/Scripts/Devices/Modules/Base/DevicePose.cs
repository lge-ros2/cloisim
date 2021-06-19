/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class DevicePose
{
	private bool isSubParts = false;

	private Pose deviceModelPose = Pose.identity;
	private Pose deviceLinkPose = Pose.identity;
	private Pose devicePose = Pose.identity;

	public bool SubParts
	{
		set => isSubParts = value;
		get => isSubParts;
	}

	public void Store(in Transform targetTransform)
	{
		// Debug.Log(deviceName + ":" + transform.name);
		var devicePosition = Vector3.zero;
		var deviceRotation = Quaternion.identity;

		var parentLinkObject = targetTransform.parent;
		if (parentLinkObject != null && parentLinkObject.CompareTag("Link"))
		{
			deviceLinkPose.position = parentLinkObject.localPosition;
			deviceLinkPose.rotation = parentLinkObject.localRotation;
			// Debug.Log(parentLinkObject.name + ": " + deviceLinkPose.position.ToString("F4") + ", " + deviceLinkPose.rotation.ToString("F4"));

			var parentModelObject = parentLinkObject.parent;
			if (parentModelObject != null && parentModelObject.CompareTag("Model"))
			{
				deviceModelPose.position = parentModelObject.localPosition;
				deviceModelPose.rotation = parentModelObject.localRotation;
				// Debug.Log(parentModelObject.name + ": " + deviceModelPose.position.ToString("F4") + ", " + deviceModelPose.rotation.ToString("F4"));
			}
		}

		devicePose.position = targetTransform.localPosition;
		devicePose.rotation = targetTransform.localRotation;
	}

	public Pose Get()
	{
		var finalPose = devicePose;

		if (!isSubParts)
		{
			finalPose.position += deviceLinkPose.position;
			finalPose.rotation *= deviceLinkPose.rotation;

			finalPose.position += deviceModelPose.position;
			finalPose.rotation *= deviceModelPose.rotation;
		}
		// Debug.Log(name + ": " + finalPose.position.ToString("F4") + ", " + finalPose.rotation.ToString("F4"));

		return finalPose;
	}
}