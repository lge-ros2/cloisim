/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class DevicePose
{
	private bool _isSubParts = false;

	private Pose _deviceModelPose = Pose.identity;
	private Pose _deviceLinkPose = Pose.identity;
	private Pose _devicePose = Pose.identity;

	public bool SubParts
	{
		set => _isSubParts = value;
		get => _isSubParts;
	}

	public void Store(in Transform targetTransform)
	{
		// Debug.Log($"{targetTransform.name}");

		var parentLinkObject = targetTransform.parent;
		if (parentLinkObject != null && parentLinkObject.CompareTag("Link"))
		{
			_deviceLinkPose.position = parentLinkObject.localPosition;
			_deviceLinkPose.rotation = parentLinkObject.localRotation;
			// Debug.Log($"{parentLinkObject.name}: {parentLinkObject.position.ToString("F4")}, {parentLinkObject.rotation.ToString("F4")}");

			var parentModelObject = parentLinkObject.parent;
			if (parentModelObject != null && parentModelObject.CompareTag("Model"))
			{
				_deviceModelPose.position = parentModelObject.localPosition;
				_deviceModelPose.rotation = parentModelObject.localRotation;
				// Debug.Log($"{parentModelObject.name}: {_deviceModelPose.position.ToString("F4")}, {_deviceModelPose.rotation.ToString("F4")}");
			}
		}

		_devicePose.position = targetTransform.localPosition;
		_devicePose.rotation = targetTransform.localRotation;
	}

	public Pose Get()
	{
		var finalPose = _devicePose;

		if (!_isSubParts)
		{
			finalPose.position += _deviceLinkPose.position;
			finalPose.rotation *= _deviceLinkPose.rotation;

			finalPose.position += _deviceModelPose.position;
			finalPose.rotation *= _deviceModelPose.rotation;
		}
		// Debug.Log(name + ": " + finalPose.position.ToString("F4") + ", " + finalPose.rotation.ToString("F4"));

		return finalPose;
	}
}