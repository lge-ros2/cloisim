/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class Motor : MonoBehaviour
{
	private PID pidControl = null;
	private HingeJoint joint = null;
	private Rigidbody _motorBody;

	private float _lastAngle = 0f;
	private float _targetAngularVelocity = 0f;
	private float _targetAngularVelocityCompensation = 0f;

	private float _commandForce = 0f;
	private bool _enableMotor = false;

	private int _maxStopTry = 20;
	private int _stopCount = 0;
	private bool _directionSwitched = false;

	public string GetMotorName()
	{
		return (_motorBody == null)? string.Empty:_motorBody.transform.parent.name;
	}

	public void SetTargetJoint(in HingeJoint targetJoint)
	{
		joint = targetJoint;
		joint.useMotor = true;
		_motorBody = joint.GetComponent<Rigidbody>();
	}

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		pidControl = new PID(pFactor, iFactor, dFactor);
	}

	public PID GetPID()
	{
		return pidControl;
	}

	/// <summary>Get Current Joint Velocity</summary>
	/// <remarks>degree per second</remarks>
	public float GetCurrentVelocity()
	{
		return (joint)? (joint.velocity):0;
	}

	/// <summary>Set Target Velocity with PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetVelocityTarget(in float targetAngularVelocity)
	{
		_lastAngle = joint.angle;

		if (targetAngularVelocity.Equals(float.Epsilon) || targetAngularVelocity == 0f)
		{
			_enableMotor = false;
			// _commandForce = 0f;
			_targetAngularVelocity = 0f;
		}
		else
		{
			_enableMotor = true;

			if (_targetAngularVelocity != 0)
			{
				if (Mathf.Sign(_targetAngularVelocity) == Mathf.Sign(targetAngularVelocity))
				{
					_directionSwitched = false;
				}
				else
				{
					_stopCount = _maxStopTry;
					_directionSwitched = true;
					Debug.Log(GetMotorName() + " - direction switched");
				}
			}

			_targetAngularVelocity = targetAngularVelocity ;
		}

		pidControl.Reset();
	}

	void FixedUpdate()
	{
		if (joint == null)
		{
			return;
		}

		var motor = joint.motor;

		var currentAngle = joint.angle + 180f;
		var rotatedAngle = _lastAngle - currentAngle;
		_lastAngle = currentAngle;
		
		var currentVelocity = rotatedAngle / Time.fixedDeltaTime;

		// Compensate target angular velocity
		if (_targetAngularVelocity != 0)
		{
			if (((currentVelocity > 0) == (_targetAngularVelocity > 0) && currentVelocity < _targetAngularVelocity) ||
				((currentVelocity < 0) == (_targetAngularVelocity < 0) && currentVelocity > _targetAngularVelocity))
			{
				// Debug.Log(GetMotorName() + "_test: it low speed");
				_targetAngularVelocityCompensation += 1f;
				// Debug.Log(GetMotorName() + "_test: " + joint.angle + "-> " + rotatedAngle + ", (" + _targetAngularVelocityCompensation + ") "+ (currentVelocity) + " < " + (_targetAngularVelocity));
			}
			else
			{
				// Debug.Log(GetMotorName() + "_test: reached target speed speed");
				_targetAngularVelocityCompensation -= 1f;

				if (_targetAngularVelocityCompensation < 0)
					_targetAngularVelocityCompensation = 0;
			}
		}
		else
		{
			_targetAngularVelocityCompensation = 0;
		}

		var compensatedTargetAngularVelocity = _targetAngularVelocity + Mathf.Sign(_targetAngularVelocity) * _targetAngularVelocityCompensation;

		// do stop motion of motor when motor disabled
		if (!_enableMotor)
		{
			Stop();
		}
		else
		{
			var targetAngle = _targetAngularVelocity * Time.fixedDeltaTime;
			_commandForce = pidControl.Update(targetAngle, rotatedAngle, Time.fixedDeltaTime);

			Debug.Log(GetMotorName() + ", " + _targetAngularVelocity + " + " + _targetAngularVelocityCompensation + " = " + compensatedTargetAngularVelocity);

			// Improve motion for rapid direction change
			if (_directionSwitched)
			{
				Stop();

				compensatedTargetAngularVelocity = 0;
				// _commandForce = 0;

				if (_stopCount-- == 0)
				{
					_directionSwitched = false;
				}
			}
		}

		// targetVelocity angular velocity in degrees per second.
		motor.targetVelocity = compensatedTargetAngularVelocity;
		motor.force = _commandForce;

		// Should set the JointMotor value to update
		joint.motor = motor;
	}

	public void Stop()
	{
		_motorBody.velocity = Vector3.up * _motorBody.velocity.y;
		_motorBody.angularVelocity = Vector3.zero;
		// Debug.Log(GetMotorName() + ": vel " + _motorBody.velocity + ", angvel " + _motorBody.angularVelocity);

		_commandForce = 0;
	}
}