/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
// #define PRINT_COMMAND_LOG

using System.Collections.Generic;
using System.Text;
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

			public Command(in Articulation joint, in float targetPosition_, in float targetVelocity_)
			{
				this.joint = joint;
				this.targetPosition = float.NaN;
				this.targetVelocity = float.NaN;
				Set(targetPosition_, targetVelocity_);
			}

			public void Set(in float targetPosition_, in float targetVelocity_)
			{
				if (targetPosition_ != float.NaN)
					this.targetPosition = (this.joint.IsRevoluteType() ? SDF2Unity.CurveOrientation(targetPosition_) : targetPosition_);

				if (targetVelocity_ != float.NaN)
					this.targetVelocity = (this.joint.IsRevoluteType() ? SDF2Unity.CurveOrientation(targetVelocity_) : targetVelocity_);
			}
		}

#if PRINT_COMMAND_LOG
		private StringBuilder commandLog = new StringBuilder();
#endif

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
			if (PopDeviceMessage<messages.JointCmdV>(out var jointCommandV))
			{
				if (jointCommandV == null)
				{
					Debug.LogWarning("JointCommand: Pop Message failed.");
					return;
				}
#if PRINT_COMMAND_LOG
				commandLog.Clear();
#endif
				foreach (var jointCommand in jointCommandV.JointCmds)
				{
					var jointName = jointCommand.Name;
					var articulation = jointState.GetArticulation(jointName);
					if (articulation != null)
					{
						var targetPosition = float.NaN;
						if (jointCommand.Position != null)
						{
							targetPosition = (float)jointCommand.Position.Target;
#if PRINT_COMMAND_LOG
							commandLog.AppendLine(jointName + ": targetPosition=" + targetPosition);
#endif
						}

						var targetVelocity = float.NaN;
						if (jointCommand.Velocity != null)
						{
							targetVelocity = (float)jointCommand.Velocity.Target;
#if PRINT_COMMAND_LOG
							commandLog.AppendLine(jointName + ": targetVelocity=" + targetVelocity);
#endif
						}

						var newCommand = new Command(articulation, targetPosition, targetVelocity);
						jointCommandQueue.Enqueue(newCommand);
					}
				}
#if PRINT_COMMAND_LOG
				if (commandLog.Length > 0)
					Debug.Log(commandLog.ToString());
#endif
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
				// Debug.Log(command.targetVelocity + "," + command.targetPosition);
				command.joint.Drive(command.targetVelocity, command.targetPosition);
			}
		}
	}
}