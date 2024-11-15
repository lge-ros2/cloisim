/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : Articulation
{
	private const float WheelResolution = 0.043945312f; // in degree, encoding 13bits, 360°

	private PID _pidControl = null;
	private float _targetAngularVelocity = 0; // degree per seconds
	private float _currentMotorVelocity = 0; // degree per seconds

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
		DriveType = ArticulationDriveType.Force;

		CheckDriveType();
	}

	public override void Reset()
	{
		// Debug.Log("Motor Reset");
		base.Reset();

		if (_pidControl != null)
		{
			_pidControl.Reset();
		}
		_prevJointPosition = 0;
	}

	public void SetPID(
		in float pFactor, in float iFactor, in float dFactor,
		in float integralMin, in float integralMax,
		in float outputMin, in float outputMax)
	{
		if (_pidControl == null)
		{
			_pidControl = new PID(
				pFactor, iFactor, dFactor,
				integralMin, integralMax,
				outputMin, outputMax);
		}
		else
		{
			_pidControl.Change(pFactor, iFactor, dFactor);
		}
	}

	private void CheckDriveType()
	{
		if (DriveType is ArticulationDriveType.Force)
		{
			if (_jointBody.xDrive.damping < float.Epsilon &&
				_jointBody.yDrive.damping < float.Epsilon &&
				_jointBody.zDrive.damping < float.Epsilon)
			{
				Debug.LogWarning("Force DriveType requires valid damping > 0, Forcefully set to target velocity.");
				DriveType = ArticulationDriveType.Velocity;
			}
		}
	}

	/// <summary>Get Current Joint angular Velocity</summary>
	/// <remarks>radian per second</remarks>
	public float GetAngularVelocity()
	{
		return GetVelocity() * Mathf.Deg2Rad;
	}

	/// <summary>Set Target Velocity wmotorLeftith PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetTargetVelocity(in float angularVelocity)
	{
		_targetAngularVelocity = angularVelocity;
	}

	public double UpdatePID(in double actual, in double target, in double duration)
	{
		return (_pidControl != null) ? _pidControl.Update(actual, target, duration) : 0;
	}

	public void Run(in float duration)
	{
		var adjustValue = UpdatePID(_currentMotorVelocity, _targetAngularVelocity, duration);
		var targetVelocity = _targetAngularVelocity + (float)adjustValue;
		// Debug.Log($"{_jointBody.name} currentMotorVelocity: {_currentMotorVelocity} targetVelocity: {_targetAngularVelocity} {adjustValue} => {targetVelocity}");
		Drive(targetVelocity: targetVelocity);
	}

	private float _prevJointPosition = float.NaN; // in deg, for GetAngularVelocity()
	private double _prevTimeStamp = double.NaN;

	public float GetVelocity()
	{
		if (float.IsNaN(_prevJointPosition) || double.IsNaN(_prevTimeStamp))
		{
			_prevJointPosition = GetJointPosition() * Mathf.Rad2Deg;
			_prevTimeStamp = Time.timeAsDouble;
			_currentMotorVelocity = 0;
		}
		else if (System.Math.Abs(Time.timeAsDouble - _prevTimeStamp) > float.Epsilon)
		{
			var timeDelta = Time.timeAsDouble - _prevTimeStamp;

			var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
			var motorVelocity = (float)(Mathf.DeltaAngle(_prevJointPosition, jointPosition) / timeDelta);
			var sampledVelocity = Mathf.Sign(motorVelocity) * Mathf.Floor(Mathf.Abs(motorVelocity) / WheelResolution) * WheelResolution;
			// Debug.LogFormat("prv={0:F5} cur={1:F5} vel={2:F5} sampVel={3:F5}", _prevJointPosition, jointPosition, motorVelocity, sampledVelocity);

			_prevJointPosition = jointPosition;
			_prevTimeStamp = Time.timeAsDouble;

			_currentMotorVelocity = (Mathf.Abs(sampledVelocity) < Quaternion.kEpsilon) ? 0 : sampledVelocity;
		}

		return _currentMotorVelocity;
	}
}