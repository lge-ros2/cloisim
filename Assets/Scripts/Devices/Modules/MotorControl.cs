/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;


public class MotorControl
{
	public enum WheelLocation { NONE, LEFT, RIGHT, REAR_LEFT, REAR_RIGHT };

#region Motor Related
	private Dictionary<WheelLocation, Motor> wheelList = new Dictionary<WheelLocation, Motor>()
	{
		{WheelLocation.LEFT, null},
		{WheelLocation.RIGHT, null},
		{WheelLocation.REAR_LEFT, null},
		{WheelLocation.REAR_RIGHT, null}
	};

	private float pidGainP, pidGainI, pidGainD;

	private Odometry odometry = null;
#endregion

	public void Reset()
	{
		if (odometry != null)
		{
			odometry.Reset();
		}
	}

	public void SetWheelInfo(in float radius, in float tread)
	{
		this.odometry = new Odometry(radius, tread);
		this.odometry.SetMotorControl(this);
	}

	public void SetPID(in float p, in float i, in float d)
	{
		pidGainP = p;
		pidGainI = i;
		pidGainD = d;
	}

	public void AddWheelInfo(in WheelLocation location, in GameObject targetMotorObject)
	{
		var motor = new Motor(targetMotorObject);
		motor.SetPID(pidGainP, pidGainI, pidGainD);

		wheelList[location] = motor;
	}

	/// <summary>Set differential driver</summary>
	/// <remarks>rad per second for wheels</remarks>
	public void SetDifferentialDrive(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var angularVelocityLeft = SDF2Unity.CurveOrientation(linearVelocityLeft * odometry.InverseWheelRadius);
		var angularVelocityRight = SDF2Unity.CurveOrientation(linearVelocityRight * odometry.InverseWheelRadius);

		SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
	}

	public void SetTwistDrive(in float linearVelocity, in float angularVelocity)
	{
		// m/s, rad/s
		// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * odometry.WheelTread * 0.5f);

		var linearVelocityLeft = linearVelocity - angularCalculation;
		var linearVelocityRight = linearVelocity + angularCalculation;

		SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
	}

	public void UpdateMotorFeedback(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var linearVelocity = (linearVelocityLeft + linearVelocityRight) * 0.5f;
		var angularVelocity = (linearVelocityRight - linearVelocity) / (odometry.WheelTread * 0.5f);

		UpdateTargetMotorFeedback(angularVelocity);
	}

	public void UpdateTargetMotorFeedback(in float angularVelocity)
	{
		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Feedback.SetRotatingTargetVelocity(angularVelocity);
			}
		}
	}

	public void UpdateCurrentMotorFeedback(in float angularVelocity)
	{
		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Feedback.SetRotatingVelocity(angularVelocity);
			}
		}
	}

	/// <summary>Set motor velocity</summary>
	/// <remarks>degree per second</remarks>
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		var isRotating = (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight));

		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Feedback.SetMotionRotating(isRotating);

				if (wheel.Key.Equals(WheelLocation.LEFT) || wheel.Key.Equals(WheelLocation.REAR_LEFT))
				{
					motor.SetVelocityTarget(angularVelocityLeft);
				}

				if (wheel.Key.Equals(WheelLocation.RIGHT) || wheel.Key.Equals(WheelLocation.REAR_RIGHT))
				{
					motor.SetVelocityTarget(angularVelocityRight);
				}
			}
		}
	}

	public bool GetCurrentVelocity(in WheelLocation location, out float angularVelocity)
	{
		angularVelocity = 0;
		var motor = wheelList[location];
		if (motor != null)
		{
			angularVelocity = motor.GetCurrentVelocity();
			return true;
		}
		return false;
	}

	public void UpdateTime(in float duration)
	{
		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Update(duration);
			}
		}
	}

	public bool UpdateOdometry(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		return (odometry != null) ? odometry.Update(odomMessage, duration, imuSensor) : false;
	}
}