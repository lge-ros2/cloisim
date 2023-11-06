/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : Articulation
{
	public class RapidChangeControl
	{
		private const int _maxWaitCount = 30;
		private bool _directionSwitched = false;
		private int _waitForStopCount = 0;

		public void SetDirectionSwitched(in bool switched)
		{
			// if (switched)
			// {
			// 	Debug.Log(GetMotorName() + " - direction switched");
			// }

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
		public float compensatingVelocityIncrease = 0.20f;
		public float compensatingVelocityDecrease = 0.60f;

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

	private RapidChangeControl _rapidControl = new RapidChangeControl();
	private MotorMotionFeedback _feedback = new MotorMotionFeedback();
	public MotorMotionFeedback Feedback => _feedback;

	private PID _pidControl = null;

	private bool _enableMotor = false;

	private float _targetAngularVelocity = 0;
	private float _currentMotorVelocity = 0; // degree per seconds
	private float _prevJointPosition = 0;

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
	}

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		_pidControl = new PID(pFactor, iFactor, dFactor, 50, -50, 300, -300);
	}

	/// <summary>Get Current Joint Velocity</summary>
	/// <remarks>radian per second</remarks>
	public float GetCurrentVelocity()
	{
		return _currentMotorVelocity * Mathf.Deg2Rad;
	}

	/// <summary>Set Target Velocity wmotorLeftith PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetVelocityTarget(in float targetAngularVelocity)
	{
		var compensatingVelocityRatio = 0f;

		if (Mathf.Abs(targetAngularVelocity) < Quaternion.kEpsilon)
		{
			_enableMotor = false;
		}
		else
		{
			_enableMotor = true;

			if (_feedback.IsMotionRotating)
			{
				var directionSwitch = (Mathf.Sign(_targetAngularVelocity) == Mathf.Sign(targetAngularVelocity)) ? false : true;
				_rapidControl.SetDirectionSwitched(directionSwitch);
			}

			const float compensateThreshold = 10.0f;
			const float compensatingRatio = 1.20f;

			compensatingVelocityRatio = ((Mathf.Abs(targetAngularVelocity) < compensateThreshold) ? compensatingRatio : 1.0f);
		}

		_targetAngularVelocity = targetAngularVelocity * compensatingVelocityRatio;
	}

	public void Update(in float duration)
	{
		if (!IsRevoluteType())
		{
			// Debug.LogWarningFormat("joint type({0}) is not 'revolute'!!", Type);
			return;
		}

		_currentMotorVelocity = GetMotorVelocity(duration);
		// Debug.LogFormat("joint vel({0}) accel({1}) force({2}) friction({3}) pos({4})",
		// 	Body.jointVelocity[0], Body.jointAcceleration[0], Body.jointForce[0], Body.jointFriction, Body.jointPosition[0]);

		// do stop motion of motor when motor disabled
		if (_enableMotor)
		{
			// Compensate target angular velocity
			var targetAngularVelocityCompensation = (_targetAngularVelocity != 0) ? (Mathf.Sign(_targetAngularVelocity) * _feedback.Compensate()) : 0;

			var compensatedTargetAngularVelocity = _targetAngularVelocity + targetAngularVelocityCompensation;

			var adjustValue = _pidControl.Update(compensatedTargetAngularVelocity, _currentMotorVelocity, duration);

			// Improve motion for rapid direction change
			if (_rapidControl.DirectionSwitched())
			{
				Stop();
				compensatedTargetAngularVelocity = 0;
				_rapidControl.Wait();
			}
			else
			{
				Drive(compensatedTargetAngularVelocity + adjustValue);
			}
		}
		else
		{
			Stop();
		}
	}

	public void Stop()
	{
		Drive(0);
		SetJointVelocity(0);

		_pidControl.Reset();
		_rapidControl.SetDirectionSwitched(false);

		Reset();
	}

	private float GetMotorVelocity(in float duration)
	{
		// calculate velocity using joint position is more accurate than joint velocity
		var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
		var motorVelocity = Mathf.DeltaAngle(_prevJointPosition, jointPosition) / duration;
		_prevJointPosition = jointPosition;

		return (Mathf.Approximately(motorVelocity, Quaternion.kEpsilon)) ? 0 : motorVelocity;
	}
}