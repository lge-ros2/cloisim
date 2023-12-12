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
		private bool _directionSwitched = false;

		public void SetDirectionSwitched(in bool switched)
		{
			_directionSwitched = switched;
		}

		public bool DirectionSwitched()
		{
			return _directionSwitched;
		}

		public void Wait(in float velocity)
		{
			// wait until velocity is zero
			if (Mathf.Abs(velocity) <= Quaternion.kEpsilon)
			{
				SetDirectionSwitched(false);
			}
		}
	}

	public class MotorMotionFeedback
	{
		private bool _isRotating = false;

		public bool IsMotionRotating => _isRotating;

		public void SetMotionRotating(in bool enable)
		{
			_isRotating = enable;
		}
	}

	private RapidChangeControl _rapidControl = new RapidChangeControl();
	private MotorMotionFeedback _feedback = new MotorMotionFeedback();
	public MotorMotionFeedback Feedback => _feedback;

	private PID _pidControl = null;

	private bool _enable = false;

	private const float WheelResolution = 0.087890625f; // in degree, encoding 12bits,360°

	private float _targetAngularVelocity = 0;
	private float _currentMotorVelocity = 0; // degree per seconds

	public Motor(in GameObject gameObject)
		: base(gameObject)
	{
	}

	new public void Reset()
	{
		base.Reset();

		_pidControl.Reset();
		_rapidControl.SetDirectionSwitched(false);
		_prevJointPosition = 0;
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
			_enable = false;
		}
		else
		{
			_enable = true;

			if (_feedback.IsMotionRotating)
			{
				if (Mathf.Abs(_targetAngularVelocity) < Quaternion.kEpsilon)
				{
					_rapidControl.SetDirectionSwitched(false);
				}
				else
				{
					var directionSwitch = (Mathf.Sign(_targetAngularVelocity) == Mathf.Sign(targetAngularVelocity)) ? false : true;
					Debug.Log("isRotation " + directionSwitch);
					_rapidControl.SetDirectionSwitched(directionSwitch);
				}
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
		if (_enable)
		{
			// Improve motion for rapid direction change
			if (_rapidControl.DirectionSwitched())
			{
				var decelVelocity = GetDecelerationVelocity(10);
				_rapidControl.Wait(decelVelocity);

				if (_rapidControl.DirectionSwitched() == false)
				{
					_pidControl.Reset();
				}
				else
				{
					Stop(decelVelocity);
				}
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
			// Debug.Log("Update disable motor :" + _currentMotorVelocity.ToString("F5") + ", " + decelVelocity.ToString("F6"));
			Stop(decelVelocity);
		}
	}

	// in Deg
	private float GetDecelerationVelocity(float DecreasingVelocityLevel = 30f)
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
		// Debug.Log("Stop :" + decelerationVelocity.ToString("F6"));
		Drive(decelerationVelocity);

		if (Mathf.Abs(decelerationVelocity) < Quaternion.kEpsilon)
		{
			_pidControl.Reset();
			_rapidControl.SetDirectionSwitched(false);
			base.Reset();
		}
	}

	private float _prevJointPosition = 0; // in deg, for GetAngularVelocity()

	/// <remarks>degree per second</remarks>
	private float GetAngularVelocity(in float duration)
	{
		var motorVelocity = 0f;
		if (Mathf.Approximately(Mathf.Abs(_targetAngularVelocity), Quaternion.kEpsilon) == false)
		{
			// calculate velocity using joint position is more accurate than joint velocity
			var jointPosition = GetJointPosition() * Mathf.Rad2Deg;
			motorVelocity = Mathf.DeltaAngle(_prevJointPosition, jointPosition) / duration;
			// Debug.LogFormat("prv:{0:F5} cur:{1:F5} vel:{2:F5}", _prevJointPosition, jointPosition, motorVelocity);
			_prevJointPosition = jointPosition;
		}

		var sampledVelocity = Mathf.Sign(motorVelocity) * Mathf.Floor(Mathf.Abs(motorVelocity) / WheelResolution) * WheelResolution;
		// Debug.LogFormat("motvel:{0:F5} filvel:{1:F5}", motorVelocity, sampledVelocity);

		return (Mathf.Abs(sampledVelocity) < Quaternion.kEpsilon) ? 0 : sampledVelocity;
	}
}