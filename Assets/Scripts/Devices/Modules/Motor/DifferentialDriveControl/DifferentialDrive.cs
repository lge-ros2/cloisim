/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;


public class DifferentialDrive : MotorControl
{
	private Odometry _odometry = null;

	public DifferentialDrive(in Transform controllerTransform)
		: base(controllerTransform)
	{
	}

	public override void Reset()
	{
		_odometry?.Reset();

		foreach (var motor in _motorList)
		{
			motor.Value?.Reset();
		}
	}

	public override void SetWheelInfo(in float radius, in float separation)
	{
		this._odometry = new Odometry(radius, separation);
	}

	public override void Drive(in float linearVelocity, in float angularVelocity)
	{
		// m/s, rad/s
		// var linearVelocityLeft = ((2 * linearVelocity) - (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * _odometry.WheelSeparation * 0.5f);

		// Velocity(rad per second) for wheels
		var linearVelocityLeft = (linearVelocity - angularCalculation) * _odometry.InverseWheelRadius;
		var linearVelocityRight = (linearVelocity + angularCalculation) * _odometry.InverseWheelRadius;

		var angularVelocityLeft = SDF2Unity.CurveOrientation(linearVelocityLeft);
		var angularVelocityRight = SDF2Unity.CurveOrientation(linearVelocityRight);

		SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
	}

	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		foreach (var elem in _motorList)
		{
			var location = elem.Key;
			var motor = elem.Value;

			if (location.Equals(Location.FRONT_WHEEL_RIGHT) || location.Equals(Location.REAR_WHEEL_RIGHT))
			{
				motor?.SetTargetVelocity(angularVelocityRight);
			}

			if (location.Equals(Location.FRONT_WHEEL_LEFT) || location.Equals(Location.REAR_WHEEL_LEFT))
			{
				motor?.SetTargetVelocity(angularVelocityLeft);
			}
		}
	}

	public override bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor = null)
	{
		foreach (var motor in _motorList)
		{
			motor.Value?.Run(duration);
		}

		var angularVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var angularVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

		return (_odometry != null) ? _odometry.Update(odomMessage, angularVelocityLeft, angularVelocityRight, duration, imuSensor) : false;
	}
}