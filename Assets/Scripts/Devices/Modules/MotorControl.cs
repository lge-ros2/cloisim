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

	#region <Motor Related>
	private Dictionary<WheelLocation, Motor> wheelList = new Dictionary<WheelLocation, Motor>()
	{
		{WheelLocation.LEFT, null},
		{WheelLocation.RIGHT, null},
		{WheelLocation.REAR_LEFT, null},
		{WheelLocation.REAR_RIGHT, null}
	};

	private float _pidGainP, _pidGainI, _pidGainD;

	private Odometry odometry = null;
	#endregion

	public void Reset()
	{
		if (odometry != null)
		{
			odometry.Reset();
		}

		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Reset();
			}
		}
	}

	public void SetWheelInfo(in float radius, in float separation)
	{
		this.odometry = new Odometry(this, radius, separation);
	}

	public void SetPID(in float p, in float i, in float d)
	{
		_pidGainP = p;
		_pidGainI = i;
		_pidGainD = d;
	}

	public void AttachWheel(in WheelLocation location, in GameObject targetMotorObject)
	{
		var motor = new Motor(targetMotorObject);
		motor.SetPID(_pidGainP, _pidGainI, _pidGainD);

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
		// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * odometry.WheelSeparation * 0.5f);

		var linearVelocityLeft = linearVelocity - angularCalculation;
		var linearVelocityRight = linearVelocity + angularCalculation;
		SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
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

				if (wheel.Key.Equals(WheelLocation.RIGHT) || wheel.Key.Equals(WheelLocation.REAR_RIGHT))
				{
					motor.SetVelocityTarget(angularVelocityRight);
				}

				if (wheel.Key.Equals(WheelLocation.LEFT) || wheel.Key.Equals(WheelLocation.REAR_LEFT))
				{
					motor.SetVelocityTarget(angularVelocityLeft);
				}
			}
		}
	}

	/// <summary>Get target Motor Velocity</summary>
	/// <remarks>radian per second</remarks>
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

	public void Update(in float duration)
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