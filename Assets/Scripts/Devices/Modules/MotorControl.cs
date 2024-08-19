/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading.Tasks;
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

	public struct MotorTask
	{
		public Motor motor;
		public float targetVelocity;

		public MotorTask(Motor motor, float targetVelocity)
		{
			this.motor = motor;
			this.targetVelocity = targetVelocity;
		}
	};

	List<MotorTask> _motorTaskList = new List<MotorTask>();

	private Odometry odometry = null;

	#endregion

	private Transform _baseTransform = null;

	public MotorControl(in Transform controllerTransform)
	{
		_baseTransform = controllerTransform;

		_motorTaskList.Clear();
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

	private bool IsWheelAttached()
	{
		foreach (var wheel in wheelList)
		{
			if (wheel.Value != null)
			{
				return true;
			}
		}

		Debug.LogWarning("There is no Wheel, AttachWheel() first");
		return false;
	}

	public void SetPID(
		in float p, in float i, in float d,
		in float integralMin, in float integralMax,
		in float outputMin, in float outputMax)
	{
		if (IsWheelAttached())
		{
			if (!float.IsNaN(p) && !float.IsNaN(i) && !float.IsNaN(d) &&
				!float.IsInfinity(p) && !float.IsInfinity(i) && !float.IsInfinity(d))
			{
				foreach (var wheel in wheelList)
				{
					wheel.Value?.SetPID(p, i, d, integralMin, integralMax, outputMin, outputMax);
				}
			}
			else
			{
				Debug.LogWarning("One of PID Gain value is NaN or Infinity. Set to default value");
			}
		}
	}

	public void AttachWheel(
		in WheelLocation location,
		in GameObject targetMotorObject)
	{
		wheelList[location] =  new Motor(targetMotorObject);
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
	private void CheckRotationalBehavior(in float angularVelocityLeft, in float angularVelocityRight)
	{
		if (Mathf.Sign(angularVelocityLeft) == Mathf.Sign(angularVelocityRight))
		{
			_rotationDirection = 0;
		}
		else // (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight))
		{
			_rotationDirection = (sbyte)((angularVelocityLeft > angularVelocityRight) ? 1 : -1);
		}
	}

	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		CheckRotationalBehavior(angularVelocityLeft, angularVelocityRight);

		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				if (wheel.Key.Equals(WheelLocation.RIGHT) || wheel.Key.Equals(WheelLocation.REAR_RIGHT))
				{
					_motorTaskList.Add(new MotorTask(motor, angularVelocityRight));
				}

				if (wheel.Key.Equals(WheelLocation.LEFT) || wheel.Key.Equals(WheelLocation.REAR_LEFT))
				{
					_motorTaskList.Add(new MotorTask(motor, angularVelocityLeft));
				}
			}
		}

		Parallel.ForEach(_motorTaskList, motorTask =>
		{
			motorTask.motor.SetTargetVelocity(motorTask.targetVelocity);
		});

		_motorTaskList.Clear();
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

	public bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor = null)
	{
		foreach (var wheel in wheelList)
		{
			var motor = wheel.Value;
			if (motor != null)
			{
				motor.Update(duration);
			}
		}

		return (odometry != null) ? odometry.Update(odomMessage, duration, imuSensor) : false;
	}
}