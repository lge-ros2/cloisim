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
		private const int MaxWaitCount = 10;
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
				_waitForStopCount = MaxWaitCount;
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
			_enable = false;
		}
		else
		{
			_enable = true;

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
		if (_enable)
		{
			var adjustValue = _pidControl.Update(_targetAngularVelocity, _currentMotorVelocity, duration);

			// Improve motion for rapid direction change
			if (_rapidControl.DirectionSwitched())
			{
				_rapidControl.Wait();
				if (_rapidControl.DirectionSwitched() == false)
					Stop(0);
			}
			else
			{
				Drive(_targetAngularVelocity + adjustValue);
			}
		}
		else
		{
			const float DecreasingVelocity = 60f; // in deg

			var decelerationVelocity = _currentMotorVelocity - Mathf.Sign(_currentMotorVelocity) * DecreasingVelocity;
			if (Mathf.Abs(decelerationVelocity) <= DecreasingVelocity)
			{
				decelerationVelocity = 0;
			}
			Stop(decelerationVelocity);
		}
	}

	public void Stop(in float decelerationVelocity = 0f)
	{
		Drive(decelerationVelocity);
		SetJointVelocity(decelerationVelocity);

		if (Mathf.Abs(decelerationVelocity) < Quaternion.kEpsilon)
		{
			_pidControl.Reset();
			_rapidControl.SetDirectionSwitched(false);

			Reset();
		}
	}

	private const int RollingMeanWindowSize = 20;
	private Queue<float> _rollingMeanAnguluarVelocity = new Queue<float>(RollingMeanWindowSize);
	private float _rollingMeanSumVelocity = 0f;

	private float GetFilteredAngularVelocity(in float motorVelocity)
	{
		if (Mathf.Abs(_targetAngularVelocity) <= Quaternion.kEpsilon)
		{
			_rollingMeanAnguluarVelocity.Clear();
			_rollingMeanSumVelocity = 0;
		}

		// rolling mean filtering
		if (_rollingMeanAnguluarVelocity.Count == RollingMeanWindowSize)
		{
			_rollingMeanSumVelocity -= _rollingMeanAnguluarVelocity.Dequeue();
		}

		_rollingMeanAnguluarVelocity.Enqueue(motorVelocity);
		_rollingMeanSumVelocity += motorVelocity;

		var filteredVelocity = _rollingMeanSumVelocity / (float)_rollingMeanAnguluarVelocity.Count;

		return filteredVelocity;
	}

	private float _prevJointPosition = 0; // in deg

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

		var filteredVelocity = GetFilteredAngularVelocity(motorVelocity);

		return (Mathf.Abs(filteredVelocity) < Quaternion.kEpsilon) ? 0 : filteredVelocity;
	}
}