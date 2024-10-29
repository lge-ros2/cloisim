/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;


public abstract class MotorControl
{
	public enum Location
	{
		NONE,
		FRONT_WHEEL_LEFT = 1,	FRONT_WHEEL_RIGHT = 2,
		REAR_WHEEL_LEFT = 3,	REAR_WHEEL_RIGHT = 4,
		HIP_LEFT,	HIP_RIGHT,
		LEG_LEFT,	LEG_RIGHT,
		HEAD
	};

	protected Dictionary<Location, Motor> _motorList
		= new Dictionary<Location, Motor>()
		{
			{Location.FRONT_WHEEL_LEFT, null},
			{Location.FRONT_WHEEL_RIGHT, null},
			{Location.REAR_WHEEL_LEFT, null},
			{Location.REAR_WHEEL_RIGHT, null}
		};

	protected Transform _baseTransform = null;

	public MotorControl(in Transform controllerTransform)
	{
		this._baseTransform = controllerTransform;
	}

	public abstract void Reset();
	public abstract void SetWheelInfo(in float radius, in float separation);
	public abstract void Drive(in float linearVelocity, in float angularVelocity);
	public abstract bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor = null);

	public void AttachWheel(in string wheelNameLeft, in string wheelNameRight)
	{
		AttachMotor(Location.FRONT_WHEEL_LEFT, wheelNameLeft);
		AttachMotor(Location.FRONT_WHEEL_RIGHT, wheelNameRight);
	}

	public void AttachWheel(
		in string frontWheelLeftName, in string frontWheelRightName,
		in string rearWheelLeftName, in string rearWheelRightName)
	{
		AttachWheel(frontWheelLeftName, frontWheelRightName);

		AttachMotor(Location.REAR_WHEEL_LEFT, rearWheelLeftName);
		AttachMotor(Location.REAR_WHEEL_RIGHT, rearWheelRightName);
	}

	protected void AttachMotor(in Location targetlLocation, in string targetName)
	{
		var linkHelperList = _baseTransform.GetComponentsInChildren<SDF.Helper.Link>();
		foreach (var linkHelper in linkHelperList)
		{
			// Debug.Log("AttachMotor:" + linkHelper.name + " , " + linkHelper.Model.name + " ==> " + targetName);
			if (linkHelper.name.Equals(targetName) ||
				linkHelper.JointName.Equals(targetName) ||
				linkHelper.Model.name.Equals(targetName))
			{
				var motorObject = (linkHelper.gameObject != null) ? linkHelper.gameObject : linkHelper.Model.gameObject;
				_motorList[targetlLocation] = new Motor(motorObject);
				Debug.Log($"AttachMotor: {_motorList[targetlLocation]} {targetlLocation} {targetName}");
				return;
			}
		}
	}

	public void SetWheelPID(
		float p, float i, float d,
		float integralMin, float integralMax,
		float outputMin, float outputMax)
	{
		if (_motorList[Location.FRONT_WHEEL_LEFT] != null)
			SetMotorPID(Location.FRONT_WHEEL_LEFT, p, i, d, integralMin, integralMax, outputMin, outputMax);

		if (_motorList[Location.FRONT_WHEEL_RIGHT] != null)
			SetMotorPID(Location.FRONT_WHEEL_RIGHT, p, i, d, integralMin, integralMax, outputMin, outputMax);

		if (_motorList[Location.REAR_WHEEL_LEFT] != null)
			SetMotorPID(Location.REAR_WHEEL_LEFT, p, i, d, integralMin, integralMax, outputMin, outputMax);

		if (_motorList[Location.REAR_WHEEL_RIGHT] != null)
			SetMotorPID(Location.REAR_WHEEL_RIGHT, p, i, d, integralMin, integralMax, outputMin, outputMax);
	}

	protected void SetMotorPID(
		in Location targetMotorLocation,
		in float p, in float i, in float d,
		in float integralMin, in float integralMax,
		in float outputMin, in float outputMax)
	{
		if (!float.IsNaN(p) && !float.IsNaN(i) && !float.IsNaN(d) &&
			!float.IsInfinity(p) && !float.IsInfinity(i) && !float.IsInfinity(d))
		{
			var motor = _motorList[targetMotorLocation];
			if (motor == null)
			{
				Debug.LogWarning("There is no Wheel, AttachWheel() first");
				return;
			}

			motor.SetPID(p, i, d, integralMin, integralMax, outputMin, outputMax);
		}
		else
		{
			Debug.LogWarning("One of PID Gain value is NaN or Infinity. Set to default value");
		}
	}

	/// <summary>Get target Motor Velocity</summary>
	/// <remarks>radian per second</remarks>
	protected float GetAngularVelocity(in Location location)
	{
		var motor = _motorList[location];
		return (motor == null)? float.NaN : motor.GetAngularVelocity();
	}
}
