/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : Articulation
{
	private PID _pidControl = null;

	private const float WheelResolution = 0.087890625f; // in degree, encoding 12bits,360°

	private float _targetAngularVelocity = 0; // degree per seconds
	private float _currentMotorVelocity = 0; // degree per seconds

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
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

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		_pidControl = new PID(pFactor, iFactor, dFactor, 100, -100, 1000, -1000);
	}

	/// <summary>Get Current Joint angular Velocity</summary>
	/// <remarks>radian per second</remarks>
	public float GetCurrentAngularVelocity()
	{
		return _currentMotorVelocity * Mathf.Deg2Rad;
	}

	// private bool _isRotatingMotion = false;
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
		Drive(_targetAngularVelocity + adjustValue);
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