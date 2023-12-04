/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
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
		public const float CompensatingVelocityIncrease = 0.10f;
		public const float CompensatingVelocityDecrease = 0.50f;

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
				if (IsTargetReached())
				{
					_compensateValue -= CompensatingVelocityDecrease;

					if (_compensateValue < 0)
					{
						_compensateValue = 0;
					}
				}
				else
				{
					_compensateValue += CompensatingVelocityIncrease;
					// Debug.Log("_test: it is low speed, " + _currentTwistAngularVelocity + " < " + _targetTwistAngularVelocity);
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

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
	}

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		_pidControl = new PID(pFactor, iFactor, dFactor, 10, -10, 100, -100);
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
		if (Mathf.Abs(targetAngularVelocity) < Quaternion.kEpsilon || float.IsInfinity(targetAngularVelocity))
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
		}

		_targetAngularVelocity = targetAngularVelocity;
	}

	public void Update(in float duration)
	{
		if (!IsRevoluteType())
		{
			// Debug.LogWarningFormat("joint type({0}) is not 'revolute'!!", Type);
			return;
		}

		_currentMotorVelocity = GetAngularVelocity(duration);

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
				// compensatedTargetAngularVelocity = 0;
				_rapidControl.Wait();
			}
			else
			{
				Drive(compensatedTargetAngularVelocity + adjustValue);
				// Drive(_targetAngularVelocity + adjustValue);
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

	private float _prevJointPosition = 0; // in deg
	private const int RollingMeanWindowSize = 10;
	private Queue<float> rollingMeanAnguluarVelocity = new Queue<float>(RollingMeanWindowSize);

	/// <remarks>degree per second</remarks>
	private float GetAngularVelocity(in float duration)
	{
		var motorVelocity = 0f;
		if (Mathf.Approximately(Mathf.Abs(_targetAngularVelocity), Quaternion.kEpsilon) == false)
		{
#if true
			// calculate velocity using joint position is more accurate than joint velocity
			var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
			motorVelocity = Mathf.DeltaAngle(_prevJointPosition, jointPosition) / duration;
			_prevJointPosition = jointPosition;
			// Debug.LogFormat("prv:{0:F5} cur:{1:F5} vel:{2:F5}", _prevJointPosition, jointPosition, motorVelocity);
#else
			var motorVelocity = GetJointVelocity() * Mathf.Rad2Deg;
#endif
		}

		// rolling mean filtering
		if (rollingMeanAnguluarVelocity.Count == RollingMeanWindowSize)
			rollingMeanAnguluarVelocity.Dequeue();

		rollingMeanAnguluarVelocity.Enqueue(motorVelocity);

		var sumVelocity = 0f;
		foreach (var velocity in rollingMeanAnguluarVelocity)
			sumVelocity += velocity;

		var filteredVelocity = sumVelocity / (float)rollingMeanAnguluarVelocity.Count;

		// Debug.LogFormat("{0:F5} {1:F5}", motorVelocity, filteredVelocity);

		return (Mathf.Approximately(filteredVelocity, Quaternion.kEpsilon)) ? 0 : filteredVelocity;
	}
}