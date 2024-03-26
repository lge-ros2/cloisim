/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : Articulation
{
	private PID _pidControl = null;

	private bool _enable = false;

	private const float WheelResolution = 0.087890625f; // in degree, encoding 12bits,360°

	private const float DecelAmountForRapidControl = 30;

	private float _targetAngularVelocity = 0; // degree per seconds
	private float _currentMotorVelocity = 0; // degree per seconds

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
	}

	new public void Reset()
	{
		base.Reset();

		_pidControl.Reset();
		_prevJointPosition = 0;
	}

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		_pidControl = new PID(pFactor, iFactor, dFactor, 10, -10, 100, -100);
	}

	/// <summary>Get Current Joint angular Velocity</summary>
	/// <remarks>radian per second</remarks>
	public float GetCurrentAngularVelocity()
	{
		return _currentMotorVelocity * Mathf.Deg2Rad;
	}

	public bool IsZero(in float value)
	{
		return (Mathf.Abs(value) < Quaternion.kEpsilon);
	}

	// private bool _isRotatingMotion = false;
	/// <summary>Set Target Velocity wmotorLeftith PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetTargetVelocity(in float targetAngularVelocity)
	{
		_enable = (IsZero(targetAngularVelocity) || float.IsInfinity(targetAngularVelocity)) ? false : true;
		_targetAngularVelocity = targetAngularVelocity;
	}

	public void Update(in float duration, in bool doDecreaseVelocity = true)
	{
		if (!IsRevoluteType())
		{
			// Debug.LogWarningFormat("joint type({0}) is not 'revolute'!!", Type);
			return;
		}

		SolveAngularVelocity(duration);

		// do stop motion of motor when motor disabled
		if (_enable)
		{
			if (doDecreaseVelocity)
			{
				var decelVelocity = GetDecelerationVelocity(DecelAmountForRapidControl);
				// Debug.Log("Update disable motor :" + _currentMotorVelocity.ToString("F5") + ", " + decelVelocity.ToString("F6"));
				Stop(decelVelocity);
			}
			else
			{
				var adjustValue = _pidControl.Update(_targetAngularVelocity, _currentMotorVelocity, duration);
				Drive(_targetAngularVelocity + adjustValue);
			}
		}
		else
		{
			var decelVelocity = GetDecelerationVelocity();
			// Debug.Log("decelVelocity current:" + _currentMotorVelocity.ToString("F5") + ", decel: " + decelVelocity.ToString("F6"));
			Stop(decelVelocity);
		}
	}

	// in Deg
	private float GetDecelerationVelocity(in float DecreasingVelocityLevel = 10f)
	{
		var decelerationVelocity = _currentMotorVelocity - Mathf.Sign(_currentMotorVelocity) * DecreasingVelocityLevel;

		if (Mathf.Abs(decelerationVelocity) <= DecreasingVelocityLevel)
		{
			decelerationVelocity = 0;
		}

		return decelerationVelocity;
	}

	private void Stop(in float decelerationVelocity = 0f)
	{
		Drive(decelerationVelocity);

		if (IsZero(decelerationVelocity))
		{
			_pidControl.Reset();
			base.Reset();
		}
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