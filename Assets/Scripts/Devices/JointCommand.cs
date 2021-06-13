/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class JointCommand : Device
	{
		private JointState jointState = null;
		private List<string> jointControlLinkNames = new List<string>();

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
				var jointControl = jointState.GetJointControl(linkName);

				if (jointControl != null)
				{
					var effort = (float)jointCommand.Force;
					var targetPosition = (float)jointCommand.Position.Target;
					var targetVelocity = (float)jointCommand.Velocity.Target;
					var duration = (float)(targetPosition / targetVelocity);

					// jointControl.SetCommand(effort, targetPosition, targetVelocity, duration);

					if (!jointControlLinkNames.Contains(linkName))
					{
						jointControlLinkNames.Add(linkName);
					}
				}
			}
		}

		public void SetJointState(in JointState jointState)
		{
			this.jointState = jointState;
		}

		void FixedUpdate()
		{
			for (var i = 0; i < jointControlLinkNames.Count; i++)
			{
				var linkName = jointControlLinkNames[i];
				var jointControl = jointState.GetJointControl(linkName);
				// jointControl.Drive();
			}
		}
	}
}