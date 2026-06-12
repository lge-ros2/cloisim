/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class DevicePose
{
	// Local pose of the device relative to its parent transform (usually a Link)
	private Pose _deviceLocalPose = Pose.identity;

	/// <summary>
	/// Must be called on Unity main thread (Update/LateUpdate/FixedUpdate).
	/// Stores device's local pose (relative to its parent).
	/// </summary>
	public void Store(in Transform targetTransform)
	{
		// Always store local pose only.
		// The TF tree already contains base_link->...->Link transforms.
		_deviceLocalPose.position = targetTransform.localPosition;
		_deviceLocalPose.rotation = targetTransform.localRotation;
	}

	/// <summary>
	/// Returns pose that should be used for TF edge:
	/// parent_frame (mount link) -> child_frame (sensor frame)
	/// </summary>
	public Pose Get()
	{
		// IMPORTANT:
		// Do NOT accumulate parent Link / Model poses here.
		// Doing so causes double-transform when the TF publisher also publishes link chain.
		return _deviceLocalPose;
	}
}