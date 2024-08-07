/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : Articulation
{
	private const float WheelResolution = 0.087890625f; // in degree, encoding 12bits,360°

	private PID _pidControl = null;
	private float _targetAngularVelocity = 0; // degree per seconds
	private float _currentMotorVelocity = 0; // degree per seconds

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
		DriveType = ArticulationDriveType.Force;

		CheckDriveType();
	}

	new public void Reset()
	{
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
				Debug.LogWarning("Force DriveType requires valid damping > 0, Forcefully set to target velocity");
				DriveType = ArticulationDriveType.Velocity;
			}
		}
	}

	/// <summary>Get Current Joint angular Velocity</summary>
	/// <remarks>radian per second</remarks>
	public float GetCurrentAngularVelocity()
	{
		return _currentMotorVelocity * Mathf.Deg2Rad;
	}

	/// <summary>Set Target Velocity wmotorLeftith PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetTargetVelocity(in float targetAngularVelocity)
	{
		_targetAngularVelocity = targetAngularVelocity;
	}

	public void Update(in float duration)
	{
		if (!IsRevoluteType())
		{
			return;
		}

		SolveAngularVelocity(duration);

		var adjustValue = (_pidControl != null) ? _pidControl.Update(_targetAngularVelocity, _currentMotorVelocity, duration) : 0;
		// Debug.Log(_targetAngularVelocity + "  ,   " + adjustValue + "  ,   " + _currentMotorVelocity);

		Drive(targetVelocity: _targetAngularVelocity + adjustValue);
	}

	private float _prevJointPosition = 0; // in deg, for GetAngularVelocity()

	/// <remarks>degree per second</remarks>
	private void SolveAngularVelocity(in float duration)
	{
		// calculate velocity using joint position is more accurate than joint velocity
		var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
		var motorVelocity = Mathf.DeltaAngle(_prevJointPosition, jointPosition) / duration;
		var sampledVelocity = Mathf.Sign(motorVelocity) * Mathf.Floor(Mathf.Abs(motorVelocity) / WheelResolution) * WheelResolution;
		// Debug.LogFormat("prv={0:F5} cur={1:F5} vel={2:F5} sampVel={3:F5}", _prevJointPosition, jointPosition, motorVelocity, sampledVelocity);
		_prevJointPosition = jointPosition;

		_currentMotorVelocity = (Mathf.Abs(sampledVelocity) < Quaternion.kEpsilon) ? 0 : sampledVelocity;
	}
}