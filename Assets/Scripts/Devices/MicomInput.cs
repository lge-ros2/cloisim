/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public class MicomInput : Device
{
	private MicomSensor micomForWheelDrive = null;

	protected override void OnAwake()
	{
		Mode = ModeType.RX_THREAD;
		DeviceName = "MicomInput";
	}

	protected override void OnStart()
	{
	}

	public void Reset()
	{
		FlushDeviceMessageQueue();
		DoWheelDrive(Vector3.zero, Vector3.zero);
	}

	protected override void ProcessDevice()
	{
		if (PopDeviceMessage<messages.Twist>(out var micomWritingData))
		{
			var linear = micomWritingData.Linear;
			var angular = micomWritingData.Angular;

			// Right-handed -> Left-handed direction of rotation
			var linearVelocity = -SDF2Unity.GetPosition(linear.X, linear.Y, linear.Z);
			var angularVelocity = -SDF2Unity.GetPosition(angular.X, angular.Y, angular.Z);

			DoWheelDrive(linearVelocity, angularVelocity);
		}
	}

	public void SetMicomSensor(in MicomSensor targetDevice)
	{
		micomForWheelDrive = targetDevice;
	}

	/// <param name="linearVelocity">m/s</param>
	/// <param name="angularVelocity">rad/s</param>
	private void DoWheelDrive(in Vector3 linearVelocity, in Vector3 angularVelocity)
	{
		if (micomForWheelDrive == null)
		{
			Debug.LogWarning("micom device for wheel drive is not ready!!");
			return;
		}

		var targetLinearVelocity = linearVelocity.z;
		var targetAngularVelocity = angularVelocity.y;
		micomForWheelDrive.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
		micomForWheelDrive.UpdateMotorFeedback(targetAngularVelocity);
	}
}