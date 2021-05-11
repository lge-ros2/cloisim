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

	private Vector3 linearVelocity = Vector3.zero; // m/s
	private Vector3 angularVelocity = Vector3.zero; // rad/s

	protected override void OnAwake()
	{
		Mode = ModeType.RX_THREAD;
		deviceName = "MicomInput";
	}

	protected override void OnStart()
	{
	}

	public void Reset()
	{
		FlushDeviceMessageQueue();
		linearVelocity = Vector3.zero;
		angularVelocity = Vector3.zero;
	}

	protected override void ProcessDevice()
	{
		DoWheelDrive();
	}

	protected override void InitializeMessages()
	{
	}

	protected override void GenerateMessage()
	{
		try
		{
			if (PopDeviceMessage<messages.Twist>(out var micomWritingData))
			{
				// Right-handed -> Left-handed direction of rotation
				linearVelocity = -SDF2Unity.GetPosition(micomWritingData.Linear.X, micomWritingData.Linear.Y, micomWritingData.Linear.Z);
				angularVelocity = -SDF2Unity.GetPosition(micomWritingData.Angular.X, micomWritingData.Angular.Y, micomWritingData.Angular.Z);
			}
		}
		catch
		{
			Debug.LogWarning("GetMessageData: ERROR");
		}
	}

	public void SetMicomSensor(in MicomSensor targetDevice)
	{
		micomForWheelDrive = targetDevice;
	}

	private void DoWheelDrive()
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