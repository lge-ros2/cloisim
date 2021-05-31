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
	public enum WheelLocation {NONE, LEFT, RIGHT};

#region Motor Related
	private Dictionary<WheelLocation, Motor> wheelList = new Dictionary<WheelLocation, Motor>()
	{
		{WheelLocation.LEFT, null},
		{WheelLocation.RIGHT, null}
	};

	private float pidGainP, pidGainI, pidGainD;

	private Micom.WheelInfo wheelInfo;
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
		wheelInfo = new Micom.WheelInfo(radius, tread);

		this.odometry = new Odometry(wheelInfo);
		this.odometry.SetMotorControl(this);
	}

	public Motor GetMotor(in WheelLocation location)
	{
		return this.wheelList[location];
	}

	public void SetPID(in float p, in float i, in float d)
	{
		pidGainP = p;
		pidGainI = i;
		pidGainD = d;
	}

	public void AddWheelInfo(in WheelLocation location, in GameObject targetMotorObject)
	{
		var motor = new Motor();
		motor.SetTargetJoint(targetMotorObject);
		motor.SetPID(pidGainP, pidGainI, pidGainD);

		wheelList[location] = motor;
	}

	/// <summary>Set differential driver</summary>
	/// <remarks>rad per second for wheels</remarks>
	public void SetDifferentialDrive(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var angularVelocityLeft = linearVelocityLeft * wheelInfo.divideWheelRadius * Mathf.Rad2Deg;
		var angularVelocityRight = linearVelocityRight * wheelInfo.divideWheelRadius * Mathf.Rad2Deg;

		SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
	}

	public void SetTwistDrive(in float linearVelocity, in float angularVelocity)
	{
		// m/s, rad/s
		// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * wheelTread)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * wheelInfo.wheelTread * 0.5f);
		var linearVelocityLeft = (linearVelocity - angularCalculation);
		var linearVelocityRight = (linearVelocity + angularCalculation);

		SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
	}

	public void UpdateMotorFeedback(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var linearVelocity = (linearVelocityLeft + linearVelocityRight) * 0.5f;
		var angularVelocity = (linearVelocityRight - linearVelocity) / (wheelInfo.wheelTread * 0.5f);

		UpdateMotorFeedback(angularVelocity);
	}

	public void UpdateMotorFeedback(in float angularVelocity)
	{
		var motorLeft = wheelList[WheelLocation.LEFT];
		var motorRight = wheelList[WheelLocation.RIGHT];

		motorLeft.Feedback.SetRotatingTargetVelocity(angularVelocity);
		motorRight.Feedback.SetRotatingTargetVelocity(angularVelocity);
	}

	/// <summary>Set motor velocity</summary>
	/// <remarks>degree per second</remarks>
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		var isRotating = (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight));

		var motorLeft = wheelList[WheelLocation.LEFT];
		var motorRight = wheelList[WheelLocation.RIGHT];

		motorLeft.Feedback.SetMotionRotating(isRotating);
		motorRight.Feedback.SetMotionRotating(isRotating);

		if (motorLeft != null)
		{
			motorLeft.SetVelocityTarget(angularVelocityLeft);
		}

		if (motorRight != null)
		{
			motorRight.SetVelocityTarget(angularVelocityRight);
		}
	}

	public bool UpdateOdometry(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		return odometry.Update(odomMessage, duration, imuSensor);
	}
}