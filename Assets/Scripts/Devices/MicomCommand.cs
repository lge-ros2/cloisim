/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class MicomCommand : Device
	{
		private MotorControl motorControl = null;

		protected override void OnAwake()
		{
			Mode = ModeType.RX_THREAD;
			DeviceName = "MicomCommand";
		}

		protected override void OnStart()
		{
		}

		protected override void OnReset()
		{
			DoWheelDrive(Vector3.zero, Vector3.zero);
		}

		public void SetMotorControl(in MotorControl motorControl)
		{
			this.motorControl = motorControl;
		}

		protected override void ProcessDevice()
		{
			if (PopDeviceMessage<messages.Twist>(out var micomWritingData))
			{
				var linear = micomWritingData.Linear;
				var angular = micomWritingData.Angular;

				var linearVelocity = SDF2Unity.GetPosition(linear.X, linear.Y, linear.Z);
				var angularVelocity = SDF2Unity.GetPosition(angular.X, angular.Y, angular.Z);

				DoWheelDrive(linearVelocity, angularVelocity);
			}
			else
			{
				Debug.LogWarning("ERROR: failed to pop deevice message");
			}
		}

		/// <param name="linearVelocity">m/s</param>
		/// <param name="angularVelocity">rad/s</param>
		private void DoWheelDrive(in Vector3 linearVelocity, in Vector3 angularVelocity)
		{
			if (motorControl == null)
			{
				Debug.LogWarning("micom device for wheel drive is not ready!!");
				return;
			}

			var targetLinearVelocity = linearVelocity.z;
			var targetAngularVelocity = angularVelocity.y;
			motorControl.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
			motorControl.UpdateMotorFeedback(targetAngularVelocity);
		}
	}
}