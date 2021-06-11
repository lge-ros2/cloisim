/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

public class Joints : Device
{
	private Dictionary<string, ArticulationBody> jointBodyTable = new Dictionary<string, ArticulationBody>();

	protected override void OnAwake()
	{
		Mode = ModeType.RX_THREAD;
		DeviceName = "Joints";
	}

	protected override void OnStart()
	{
	}

	protected override void OnReset()
	{
	}

	public bool AddTarget(in string linkName)
	{
		var childArticulationBodies = gameObject.GetComponentsInChildren<ArticulationBody>();

		foreach (var childArticulatinoBody in childArticulationBodies)
		{
			if (childArticulatinoBody.name.Equals(linkName))
			{
				jointBodyTable.Add(linkName, childArticulatinoBody);
				return true;
			}
		}

		return false;
	}

	protected override void ProcessDevice()
	{
		if (PopDeviceMessage<messages.JointCmd>(out var jointCommand))
		{
			var linear = jointCommand.Name;
			// var angular = jointCommand.Angular;

			// Right-handed -> Left-handed direction of rotation
			// var linearVelocity = -SDF2Unity.GetPosition(linear.X, linear.Y, linear.Z);
			// var angularVelocity = -SDF2Unity.GetPosition(angular.X, angular.Y, angular.Z);

			// DoWheelDrive(linearVelocity, angularVelocity);
		}
	}
}