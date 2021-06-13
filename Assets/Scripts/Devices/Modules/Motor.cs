/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Motor : JointControl
{
	public class RapidChangeControl
	{
		private bool _directionSwitched = false;
		private const int _maxWaitCount = 30;
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
	private float _targetTorque = 0;
	private float _currentMotorVelocity = 0;
	private float _prevJointPosition = 0;

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
		if (!jointType.Equals(ArticulationJointType.RevoluteJoint) && !jointType.Equals(ArticulationJointType.SphericalJoint))
		{
			Debug.LogWarningFormat("joint type({0}) is not 'revolute'!!", joint.jointType);
		}
	}

	public void SetPID(in float pFactor, in float iFactor, in float dFactor)
	{
		_pidControl = new PID(pFactor, iFactor, dFactor, 50, -50, 300, -300);
	}

	public PID GetPID()
	{
		return _pidControl;
	}

	/// <summary>Get Current Joint Velocity</summary>
	/// <remarks>degree per second</remarks>
	public float GetCurrentVelocity()
	{
		return _currentMotorVelocity;
	}

	/// <summary>Set Target Velocity wmotorLeftith PID control</summary>
	/// <remarks>degree per second</remarks>
	public void SetVelocityTarget(in float targetAngularVelocity)
	{
		var compensatingVelocityRatio = 0f;

		if (Mathf.Abs(targetAngularVelocity) < float.Epsilon || targetAngularVelocity == 0)
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

			compensatingVelocityRatio =  ((Mathf.Abs(targetAngularVelocity) < compensateThreshold) ? compensatingRatio : 1.0f);
		}

		_targetAngularVelocity = targetAngularVelocity * compensatingVelocityRatio;
	}

	public void Update()
	{
		if (this.joint == null)
		{
			Debug.LogWarning("motor Body is empty, please set target body first");
			return;
		}
		else if (!this.jointType.Equals(ArticulationJointType.RevoluteJoint) && !this.jointType.Equals(ArticulationJointType.SphericalJoint))
		{
			Debug.LogWarning("Articulation Joint Type is wrong => " + this.joint.jointType);
			return;
		}

		_currentMotorVelocity = GetMotorVelocity();
		// Debug.LogFormat("joint vel({0}) accel({1}) force({2}) friction({3}) pos({4})",
		// 	this.joint.jointVelocity[0], this.joint.jointAcceleration[0], this.joint.jointForce[0], this.joint.jointFriction, this.joint.jointPosition[0]);

		// do stop motion of motor when motor disabled
		if (_enableMotor)
		{
			// Compensate target angular velocity
			var targetAngularVelocityCompensation = (_targetAngularVelocity != 0) ? (Mathf.Sign(_targetAngularVelocity) * _feedback.Compensate()) : 0;

			var compensatedTargetAngularVelocity = _targetAngularVelocity + targetAngularVelocityCompensation;

			_targetTorque = Mathf.Abs(_pidControl.Update(compensatedTargetAngularVelocity, _currentMotorVelocity, Time.fixedDeltaTime));

			// Debug.Log(GetMotorName() + ", " + _targetAngularVelocity + " <=> " + _currentMotorVelocity);

			// Improve motion for rapid direction change
			if (_rapidControl.DirectionSwitched())
			{
				Stop();
				compensatedTargetAngularVelocity = 0;
				_rapidControl.Wait();
			}
			else
			{
				SetTargetForceAndVelocity(_targetTorque, compensatedTargetAngularVelocity);
			}
		}
		else
		{
			Stop();
		}
	}

	public void Stop()
	{
		_targetTorque = 0;

		SetJointVelocity(0);
		SetTargetForceAndVelocity(0, 0);

		_pidControl.Reset();
		_rapidControl.SetDirectionSwitched(false);

		Reset();
	}


	private void SetTargetForceAndVelocity(in float targetForce, in float targetVelocity)
	{
		Drive(targetForce, targetVelocity);
	}

	private float GetMotorVelocity()
	{
		// calculate velocity using joint position is more accurate than joint velocity
		var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
		var jointVelocity = (Mathf.DeltaAngle(_prevJointPosition, jointPosition) / Time.fixedDeltaTime);
		_prevJointPosition = jointPosition;

		return jointVelocity;
	}
}