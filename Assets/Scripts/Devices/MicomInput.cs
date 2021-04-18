/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;

public class MicomInput : Device
{
	private MicomSensor micomForWheelDrive = null;

	public enum VelocityType {Unknown, LinearAndAngular, LeftAndRight};

	private messages.Param micomWritingData = null;

	private VelocityType controlType = VelocityType.Unknown;

	public VelocityType ControlType => controlType;

	private float _wheelLinearVelocityLeft = 0; // m/s
	private float _wheelLinearVelocityRight = 0; // m/s

	private float _linearVelocity = 0; // m/s
	private float _angularVelocity = 0; // rad/s

	protected override void OnAwake()
	{
		_mode = Mode.RX;
		deviceName = "MicomInput";
	}

	protected override void OnStart()
	{
	}

	public void Reset()
	{
		ResetDataStream();
		_wheelLinearVelocityLeft = 0;
		_wheelLinearVelocityRight = 0;
		_linearVelocity = 0;
		_angularVelocity = 0;
	}

	protected override void ProcessDeviceCoroutine()
	{
		DoWheelDrive();
	}

	protected override void InitializeMessages()
	{
		micomWritingData = null;
	}

	protected override void GenerateMessage()
	{
		try
		{
			micomWritingData = GetMessageData<messages.Param>();
		}
		catch
		{
			Debug.LogWarning("GetMessageData: ERROR");
		}

		if (micomWritingData.Name.Equals("control") &&
			micomWritingData.Childrens.Count == 2)
		{
			var child0 = micomWritingData.Childrens[0];
			var child1 = micomWritingData.Childrens[1];

			// Set reversed value due to differnt direction
			// Right-handed -> Left-handed direction of rotation
			if (micomWritingData.Value.IntValue == 0)
			{
				controlType = VelocityType.LinearAndAngular;

				_linearVelocity
					= (!child0.Name.Equals("LinearVelocity")) ? 0 : (float)-child0.Value.DoubleValue;

				_angularVelocity
					= (!child1.Name.Equals("AngularVelocity")) ? 0 : (float)-child1.Value.DoubleValue;

				// Debug.Log(linearVelocity.ToString("F10") + ", " + (-child0.Value.DoubleValue).ToString("F10") + " | " + angularVelocity.ToString("F10") + ", " + (-child1.Value.DoubleValue).ToString("F10"));
			}
			else if (micomWritingData.Value.IntValue == 1)
			{
				controlType = VelocityType.LeftAndRight;

				_wheelLinearVelocityLeft
					= (!child0.Name.Equals("LeftWheelVelocity")) ? 0 : (float)-child0.Value.DoubleValue;

				_wheelLinearVelocityRight
					= (!child1.Name.Equals("RightWheelVelocity")) ? 0 : (float)-child1.Value.DoubleValue;
			}
			else
			{
				controlType = VelocityType.Unknown;
				Debug.LogWarningFormat("MicomInput: Unsupported Control Type({0}", controlType);
			}
		}
		else if (micomWritingData.Name.Equals("command"))
		{
			if (micomWritingData.Value.StringValue.Equals("reset_odometry"))
			{
				micomForWheelDrive.Reset();
				Debug.Log("MicomInput::command(reset_odometry)");
			}
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

		switch (ControlType)
		{
			case MicomInput.VelocityType.LinearAndAngular:
				var targetLinearVelocity = GetLinearVelocity();
				var targetAngularVelocity = GetAngularVelocity();
				micomForWheelDrive.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
				micomForWheelDrive.UpdateMotorFeedback(targetAngularVelocity);
				break;

			case MicomInput.VelocityType.LeftAndRight:
				var targetWheelLeftLinearVelocity = GetWheelLeftVelocity();
				var targetWheelRightLinearVelocity = GetWheelRightVelocity();
				micomForWheelDrive.SetDifferentialDrive(targetWheelLeftLinearVelocity, targetWheelRightLinearVelocity);
				micomForWheelDrive.UpdateMotorFeedback(targetWheelLeftLinearVelocity, targetWheelRightLinearVelocity);
				break;

			case MicomInput.VelocityType.Unknown:
				break;
		}
	}

	public float GetWheelLeftVelocity()
	{
		return _wheelLinearVelocityLeft;
	}

	public float GetWheelRightVelocity()
	{
		return _wheelLinearVelocityRight;
	}

	public float GetLinearVelocity()
	{
		return _linearVelocity;
	}

	public float GetAngularVelocity()
	{
		return _angularVelocity;
	}
}
