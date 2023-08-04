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
		struct Command
		{
			public Articulation joint;
			public float targetPosition;
			public float targetVelocity;

			public Command(in Articulation joint, in float targetPosition, in float targetVelocity)
			{
				this.joint = joint;
				this.targetPosition = 0;
				this.targetVelocity = 0;
				Set(targetPosition, targetVelocity);
			}

			public void Set(in float targetPosition, in float targetVelocity)
			{
				this.targetPosition = targetPosition * (this.joint.IsRevoluteType() ? Mathf.Rad2Deg : 1);
				this.targetVelocity = targetVelocity * (this.joint.IsRevoluteType() ? Mathf.Rad2Deg : 1);
			}
		}

		private JointState jointState = null;
		private Queue<Command> jointCommandQueue = new Queue<Command>();

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
				var articulation = jointState.GetArticulation(linkName);

				if (articulation != null)
				{
					var targetPosition = (float)jointCommand.Position.Target;
					var targetVelocity = (float)jointCommand.Velocity.Target;

					var newCommand = new Command(articulation, targetPosition, targetVelocity);
					jointCommandQueue.Enqueue(newCommand);
				}
			}
		}

		public void SetJointState(in JointState jointState)
		{
			this.jointState = jointState;
		}

		void FixedUpdate()
		{
			while (jointCommandQueue.Count > 0)
			{
				var command = jointCommandQueue.Dequeue();
				command.joint.Drive(command.targetVelocity, command.targetPosition);
			}
		}
	}
}