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
	private float _commandForce = 0f;
	private bool _enableMotor = false;

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
			_commandForce = 0f;
			_targetAngularVelocity = 0f;
		}
		else
		{
			_enableMotor = true;
			_targetAngularVelocity = targetAngularVelocity;
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

		var currentAngle = joint.angle;

		if (currentAngle < 0f)
		{
			currentAngle = 360f + currentAngle;
		}

		var errorAngle = _lastAngle - currentAngle;

		_lastAngle = currentAngle;

		if (_enableMotor)
		{
			var targetAngle = _targetAngularVelocity * Time.fixedDeltaTime;
			_commandForce = pidControl.Update(targetAngle, errorAngle, Time.fixedDeltaTime);
		}

		// targetVelocity angular velocity in degrees per second.
		motor.targetVelocity = _targetAngularVelocity;
		motor.force = _commandForce;

		// Should set the JointMotor value to update
		joint.motor = motor;

		if (!_enableMotor)
		{
			_motorBody.velocity = Vector3.up * _motorBody.velocity.y;
			_motorBody.angularVelocity = Vector3.zero;
			// Debug.Log(_motorBody.transform.parent.name + ": vel " + _motorBody.velocity + ", angvel " + _motorBody.angularVelocity);
		}
	}
}