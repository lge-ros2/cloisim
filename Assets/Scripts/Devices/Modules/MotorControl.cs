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

	private Transform _baseTransform = null;

	public MotorControl(in Transform controllerTransform)
	{
		_baseTransform = controllerTransform;
	}

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
	private sbyte _rotationDirection = 0;
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		if (Mathf.Sign(angularVelocityLeft) == Mathf.Sign(angularVelocityRight))
		{
			_rotationDirection = 0;
		}
		else if (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight))
		{
			_rotationDirection = (sbyte)((angularVelocityLeft > angularVelocityRight) ? 1 : -1);
		}

		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				if (wheel.Key.Equals(WheelLocation.RIGHT) || wheel.Key.Equals(WheelLocation.REAR_RIGHT))
				{
					motor.SetTargetVelocity(angularVelocityRight);
				}

				if (wheel.Key.Equals(WheelLocation.LEFT) || wheel.Key.Equals(WheelLocation.REAR_LEFT))
				{
					motor.SetTargetVelocity(angularVelocityLeft);
				}
			}
		}
	}

	/// <summary>Get target Motor Velocity</summary>
	/// <remarks>radian per second</remarks>
	public bool GetCurrentVelocity(in WheelLocation location, out float angularVelocity)
	{
		var motor = wheelList[location];
		if (motor != null)
		{
			angularVelocity = motor.GetCurrentAngularVelocity();
			return true;
		}
		angularVelocity = float.NaN;
		return false;
	}

	public float _prevPositionY = 0;

	public bool IsDirectionChanged(in float duration)
	{
		var isChanged = false;
		if (_rotationDirection != 0)
		{
			var rotationVelocity = Mathf.DeltaAngle(_prevPositionY, _baseTransform.position.y) / duration;

			Debug.Log(rotationVelocity * Mathf.Deg2Rad);

			var allVelocityStopped = (Mathf.Abs(rotationVelocity) < Vector3.kEpsilon) ? true : false;

			if (allVelocityStopped)
			{
				_rotationDirection = 0;
			}
			else
			{
				isChanged = true;
			}
		}

		_prevPositionY = _baseTransform.position.y;

		return isChanged;
	}

	public bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor = null)
	{
		var decreaseVelocity = IsDirectionChanged(duration);

		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Update(duration, decreaseVelocity);
			}
		}

		return (odometry != null) ? odometry.Update(odomMessage, duration, imuSensor) : false;
	}
}