/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class Motor : MonoBehaviour
{
	public class RapidChangeControl
	{
		private bool _directionSwitched = false;
		private const int _maxWaitCount = 20;
		private int _waitForStopCount = 0;

		public void SetDirectionSwitched(in bool switched)
		{
			_directionSwitched = switched;

			if (_directionSwitched)
			{
				_waitForStopCount = _maxWaitCount;
			}
		}

		public bool DirectionSwitched()
		{
			return _directionSwitched;
		}

		public void Wait()
		{
			if (_waitForStopCount-- <= 0)
			{
				SetDirectionSwitched(false);
			}
		}
	}

	public class MotorMotionFeedback
	{
		public float compensatingVelocityIncrease = 0.25f;
		public float compensatingVelocityDecrease = 0.65f;

		private bool _isRotating = false;
		private float _currentTwistAngularVelocity = 0;
		private float _targetTwistAngularVelocity = 0;

		private float _compensateValue = 0;

		public bool IsMotionRotating => _isRotating;

		public void SetMotionRotating(in bool enable)
		{
			_isRotating = enable;
		}

		public void SetRotatingVelocity(in float currentTwistAngularVelocity)
		{
			_currentTwistAngularVelocity = currentTwistAngularVelocity;
		}

		public void SetRotatingTargetVelocity(in float targetTwistAngularVelocity)
		{
			_targetTwistAngularVelocity = targetTwistAngularVelocity;
		}

		public bool IsTargetReached()
		{
			const float accuracy = 1000f;
			// Debug.Log(" is target reached: " + _currentTwistAngularVelocity + ", " + _targetTwistAngularVelocity);
			return ((int)Mathf.Abs(_currentTwistAngularVelocity * accuracy) >= (int)Mathf.Abs(_targetTwistAngularVelocity * accuracy));
		}

		public float Compensate()
		{
			if (IsMotionRotating)
			{
				if (IsTargetReached() == false)
				{
					_compensateValue += compensatingVelocityIncrease;
					// Debug.Log("_test: it is low speed, " + _currentTwistAngularVelocity + " < " + _targetTwistAngularVelocity);
				}
				else
				{
					_compensateValue -= compensatingVelocityDecrease;

					if (_compensateValue < 0)
					{
						_compensateValue = 0;
					}
				}
			}
			else
			{
				_compensateValue = 0;
			}

			return _compensateValue;
		}
	}

	private PID pidControl = null;
	private ArticulationBody _motorBody;

	private bool _enableMotor = false;
	private float _lastAngle = 0f;
	public float _targetAngularVelocity = 0f;

	public float currentMotorVelocity;

	public const float compensatingRatio = 1.25f; // compensting target velocity

	private RapidChangeControl _rapidControl = new RapidChangeControl();
	private MotorMotionFeedback _feedback = new MotorMotionFeedback();

	public MotorMotionFeedback Feedback => _feedback;

	public string GetMotorName()
	{
		return (_motorBody == null)? string.Empty:_motorBody.transform.parent.name;
	}

	public void SetTargetJoint(in GameObject target)
	{
		var body = target.GetComponentInChildren<ArticulationBody>();
		SetTargetJoint(body);
	}

	public void SetTargetJoint(in ArticulationBody body)
	{
		if (body.jointType.Equals(ArticulationJointType.RevoluteJoint) || body.jointType.Equals(ArticulationJointType.SphericalJoint))
		{
			_motorBody = body;
		}
		else
		{
			Debug.LogWarningFormat("joint type({0}) is not revolte!!", body.jointType);
		}
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
		// Debug.LogFormat("joint vel({0}) accel({1}) force({2}) friction({3}) pos({4})",
			// _motorBody.jointVelocity[0], _motorBody.jointAcceleration[0], _motorBody.jointForce[0], _motorBody.jointFriction, _motorBody.jointPosition[0]);
		currentMotorVelocity = (_motorBody)? (_motorBody.jointVelocity[0]):0;
		return currentMotorVelocity;
	}


	/// <summary>Set Target Velocity with PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetVelocityTarget(in float targetAngularVelocity)
	{
		// _lastAngle = joint.angle;

		if (Mathf.Abs(targetAngularVelocity) < float.Epsilon || targetAngularVelocity == 0f)
		{
			_enableMotor = false;
			_targetAngularVelocity = 0f;
		}
		else
		{
			_enableMotor = true;

			if (_targetAngularVelocity != 0 && _feedback.IsMotionRotating)
			{
				if (Mathf.Sign(_targetAngularVelocity) == Mathf.Sign(targetAngularVelocity))
				{
					_rapidControl.SetDirectionSwitched(false);
				}
				else
				{
					_rapidControl.SetDirectionSwitched(true);
					// Debug.Log(GetMotorName() + " - direction switched");
				}
			}

			const float compensateThreshold = 10.0f;

			_targetAngularVelocity = targetAngularVelocity * ((Mathf.Abs(targetAngularVelocity) < compensateThreshold)? compensatingRatio:1.0f);
		}

		pidControl.Reset();
	}

	void FixedUpdate()
	{
		// if (joint == null)
		// {
			// return;
		// }

		// var motor = joint.motor;

		// var currentAngle = joint.angle + 180f;
		// var rotatedAngle = _lastAngle - currentAngle;
		// _lastAngle = currentAngle;

		var currentVelocity = _motorBody.jointVelocity[0];

		// Compensate target angular velocity
		var targetAngularVelocityCompensation = 0f;
		if (_targetAngularVelocity != 0)
		{
			targetAngularVelocityCompensation = _feedback.Compensate();
		}

		var commandForce = 0f;
		var compensatedTargetAngularVelocity = _targetAngularVelocity + Mathf.Sign(_targetAngularVelocity) * targetAngularVelocityCompensation;

		// do stop motion of motor when motor disabled
		if (!_enableMotor)
		{
			Stop();
		}
		else
		{
			commandForce = pidControl.Update(_targetAngularVelocity, currentVelocity, Time.fixedDeltaTime);

			// Debug.Log(GetMotorName() + ", " + _targetAngularVelocity + " +- " + targetAngularVelocityCompensation + " = " + compensatedTargetAngularVelocity);

			// Improve motion for rapid direction change
			if (_rapidControl.DirectionSwitched())
			{
				Stop();
				commandForce = 0;
				compensatedTargetAngularVelocity = 0;
				_rapidControl.Wait();
			}
		}

		// targetVelocity angular velocity in degrees per second.
		var xDrive = _motorBody.xDrive;

		xDrive.targetVelocity = compensatedTargetAngularVelocity;
		xDrive.damping = commandForce;

		_motorBody.xDrive = xDrive;
		// Should set the JointMotor value to update
		// joint.motor = motor;
	}

	public void Stop()
	{
		_motorBody.velocity = Vector3.up * _motorBody.velocity.y;
		_motorBody.angularVelocity = Vector3.zero;
		// Debug.Log(GetMotorName() + ": vel " + _motorBody.velocity + ", angvel " + _motorBody.angularVelocity);
	}
}