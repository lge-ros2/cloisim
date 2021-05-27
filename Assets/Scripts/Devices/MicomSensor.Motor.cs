/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public partial class MicomSensor : Device
{
#region Motor Related
	private string wheelNameLeft = string.Empty;
	private string wheelNameRight = string.Empty;

	private Dictionary<string, Motor> _motors = new Dictionary<string, Motor>();

	public float pidGainP, pidGainI, pidGainD;

	private float wheelTread = 0.0f;
	private float wheelRadius = 0.0f;
	private float divideWheelRadius = 0.0f; // for computational performance
#endregion

	/// <summary>Set differential driver</summary>
	/// <remarks>rad per second for wheels</remarks>
	public void SetDifferentialDrive(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var angularVelocityLeft = linearVelocityLeft * divideWheelRadius * Mathf.Rad2Deg;
		var angularVelocityRight = linearVelocityRight * divideWheelRadius * Mathf.Rad2Deg;

		SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
	}

	public void SetTwistDrive(in float linearVelocity, in float angularVelocity)
	{
		// m/s, rad/s
		// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * wheelTread * 0.5f);
		var linearVelocityLeft = (linearVelocity - angularCalculation);
		var linearVelocityRight = (linearVelocity + angularCalculation);

		SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
	}

	public void UpdateMotorFeedback(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var linearVelocity = (linearVelocityLeft + linearVelocityRight) * 0.5f;
		var angularVelocity = (linearVelocityRight - linearVelocity) / (wheelTread * 0.5f);

		UpdateMotorFeedback(angularVelocity);
	}

	public void UpdateMotorFeedback(in float angularVelocity)
	{
		foreach (var motor in _motors.Values)
		{
			motor.Feedback.SetRotatingTargetVelocity(angularVelocity);
		}
	}

	/// <summary>Set motor velocity</summary>
	/// <remarks>degree per second</remarks>
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		var isRotating = (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight));

		foreach (var motor in _motors.Values)
		{
			motor.Feedback.SetMotionRotating(isRotating);
		}

		var motorLeft = _motors[wheelNameLeft];
		var motorRight = _motors[wheelNameRight];

		if (motorLeft != null)
		{
			motorLeft.SetVelocityTarget(angularVelocityLeft);
		}

		if (motorRight != null)
		{
			motorRight.SetVelocityTarget(angularVelocityRight);
		}
	}
}