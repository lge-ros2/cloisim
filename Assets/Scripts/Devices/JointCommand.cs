/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

public class JointCommand : Device
{
	private Dictionary<string, ArticulationBody> jointBodyTable = new Dictionary<string, ArticulationBody>();

	protected override void OnAwake()
	{
		Mode = ModeType.RX_THREAD;
		DeviceName = "JointCommand";
	}

	protected override void OnStart()
	{
	}

	protected override void OnReset()
	{
	}

	protected override void ProcessDevice()
	{
		if (PopDeviceMessage<messages.JointCmd>(out var jointCommand))
		{
			var linkName = jointCommand.Name;
			var effort = jointCommand.Force;
			var targetPosition = jointCommand.Position.Target;
			var targetVelocity = jointCommand.Velocity.Target;
			var duration = targetVelocity/targetPosition;

		}
	}
}