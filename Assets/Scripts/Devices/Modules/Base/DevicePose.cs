/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class DevicePose
{
	private bool isSubParts = false;

	private Transform _targetTransform = null;

	public bool SubParts
	{
		set => isSubParts = value;
		get => isSubParts;
	}

	public void Store(in Transform targetTransform)
	{
		_targetTransform = targetTransform;
	}

	public Pose Get()
	{
		if (_targetTransform == null)
		{
			return Pose.identity;
		}

		var devicePose = new Pose(_targetTransform.localPosition, _targetTransform.localRotation);

		if (!isSubParts)
		{
			var parentLinkObject = _targetTransform.parent;
			if (parentLinkObject != null && parentLinkObject.CompareTag("Link"))
			{
				devicePose.position += parentLinkObject.localPosition;
				devicePose.rotation *= parentLinkObject.localRotation;

				var parentModelObject = parentLinkObject.parent;
				if (parentModelObject != null && parentModelObject.CompareTag("Model"))
				{
					devicePose.position += parentModelObject.localPosition;
					devicePose.rotation *= parentModelObject.localRotation;
				}
			}
		}

		return devicePose;
	}
}