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
			_commandForce = 0f;
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

		var currentAngle = joint.angle;

		if (currentAngle < 0f)
		{
			currentAngle = 360f + currentAngle;
		}

		var rotatedAngle = _lastAngle - currentAngle;
		_lastAngle = currentAngle;


		var currentVelocity = rotatedAngle / Time.fixedDeltaTime;

		if (_enableMotor)
		{
			var targetAngle = _targetAngularVelocity * Time.fixedDeltaTime;
			_commandForce = pidControl.Update(targetAngle, rotatedAngle, Time.fixedDeltaTime);

			// Debug.Log(GetMotorName() + ", " + targetAngle  .ToString("F4") + " , " + errorAngle.ToString("F4") + "|" +
			// 		_targetAngularVelocity.ToString("F4") + " -> " + currentVelocity.ToString("F4") + "," + GetCurrentVelocity().ToString("F4"));
		}

		// Compensate target angular velocity
		// if (_targetAngularVelocity != 0)
		// {
		// 	if ((_targetAngularVelocity > 0 && (int)(currentVelocity) < (int)(_targetAngularVelocity)) ||
		// 		(_targetAngularVelocity < 0 && (int)(currentVelocity) > (int)(_targetAngularVelocity)))
		// 	{
		// 		Debug.Log(GetMotorName() + "_test: (" + _targetAngularVelocityCompensation + ")"+ (int)(currentVelocity) + ", " + (int)(_targetAngularVelocity ));
		// 		_targetAngularVelocityCompensation += 0.5f;
		// 	}
		// 	else
		// 	{
		// 		_targetAngularVelocityCompensation -= 0.5f;
		// 	}
		// }
		// else
		// {
		// 	Debug.Log(GetMotorName() + "_stop:" + _targetAngularVelocityCompensation.ToString("F5") + ", " );
		// 	if (_targetAngularVelocityCompensation < float.Epsilon && _targetAngularVelocityCompensation > -float.Epsilon)
		// 	{
		// 		_targetAngularVelocityCompensation = 0f;
		// 	}
		// 	else
		// 	{
		// 		_targetAngularVelocityCompensation -= 0.5f;
		// 	}
		// }

		var compensatedTargetAngularVelocity = _targetAngularVelocity + ((_targetAngularVelocity < 0)? -1:1) * _targetAngularVelocityCompensation;

		// targetVelocity angular velocity in degrees per second.l
		motor.targetVelocity = compensatedTargetAngularVelocity;
		motor.force = _commandForce;

		// do stop motion of motor when motor disabled
		if (!_enableMotor)
		{
			Stop();
		}
		else
		{
			// Improve motion for rapid direction change
			if (_directionSwitched)
			{
				Stop();

				motor.targetVelocity = 0;
				motor.force = 0;

				if (_stopCount-- == 0)
				{
					_directionSwitched = false;
				}
			}

			// Compenstate torque when target velocity is too low
			// var scale = _motorBody.angularVelocity;
			// scale.y *= comp;
			// _motorBody.angularVelocity = scale;
		}

		// Should set the JointMotor value to update
		joint.motor = motor;
	}

	public void Stop()
	{
		_motorBody.velocity = Vector3.up * _motorBody.velocity.y;
		_motorBody.angularVelocity = Vector3.zero;
		// Debug.Log(GetMotorName() + ": vel " + _motorBody.velocity + ", angvel " + _motorBody.angularVelocity);
	}
}