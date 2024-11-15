/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
#define USE_ROLLINGMEAN_FOR_ODOM

using UnityEngine;
using messages = cloisim.msgs;
using System;

public class Odometry
{
	private WheelInfo _wheelInfo;

	private float _lastImuYaw = 0f;
	private Vector3d _odomPose = Vector3d.zero;
	private double _odomTranslationalVelocity = 0;
	private double _odomRotationalVelocity = 0;

#if USE_ROLLINGMEAN_FOR_ODOM
	private const int RollingMeanWindowSize = 10;
	private RollingMean rollingMeanOdomTransVelocity = new RollingMean(RollingMeanWindowSize);
	private RollingMean rollingMeanOdomTAngularVelocity = new RollingMean(RollingMeanWindowSize);
#endif

	public float WheelSeparation => this._wheelInfo.wheelSeparation;
	public float InverseWheelRadius => this._wheelInfo.inversedWheelRadius;

	public Odometry(in float radius, in float separation)
	{
		this._wheelInfo = new WheelInfo(radius, separation);
	}

	public void Reset()
	{
		_odomTranslationalVelocity = 0;
		_odomRotationalVelocity = 0;
		_odomPose.Set(0, 0, 0);
		_lastImuYaw = 0.0f;

#if USE_ROLLINGMEAN_FOR_ODOM
		rollingMeanOdomTransVelocity.Reset();
		rollingMeanOdomTAngularVelocity.Reset();
#endif
	}

	private bool IsZero(in float value)
	{
		return Mathf.Abs(value) < Quaternion.kEpsilon;
	}

	private bool IsZero(in double value)
	{
		return Math.Abs(value) < Quaternion.kEpsilon;
	}

	/// <summary>Calculate odometry on this robot</summary>
	/// <remarks>rad per second for `angularVelocity`</remarks>
	private void CalculateOdometry(
		in float angularVelocityLeftWheel, in float angularVelocityRightWheel,
		in float duration,
		in float deltaTheta = float.NaN)
	{
		var linearVelocityLeftWheel = angularVelocityLeftWheel * _wheelInfo.wheelRadius;
		var linearVelocityRightWheel = angularVelocityRightWheel * _wheelInfo.wheelRadius;

		var sumLeftRight = linearVelocityLeftWheel + linearVelocityRightWheel;
		var diffRightLeft = linearVelocityRightWheel - linearVelocityLeftWheel;

		_odomTranslationalVelocity = IsZero(sumLeftRight) ? 0 : (sumLeftRight * 0.5f);
		_odomRotationalVelocity = IsZero(diffRightLeft) ? 0 : (diffRightLeft * _wheelInfo.inversedWheelSeparation);

		var linear = _odomTranslationalVelocity * duration;
		var angular = (float.IsNaN(deltaTheta)) ? (_odomRotationalVelocity * duration) : deltaTheta;

		// Acculumate odometry:
		if (IsZero(angular)) // RungeKutta2
		{
			var direction = _odomPose.y + angular * 0.5f;

			// Runge-Kutta 2nd order integration:
			_odomPose.z += linear * Math.Cos(direction);
			_odomPose.x += linear * Math.Sin(direction);
			_odomPose.y += angular;
			// Debug.Log("CalcOdom 0 = " + _odomPose.y + ", " + angular);
		}
		else
		{
			// Exact integration (should solve problems when angular is zero):
			var headingOld = _odomPose.y;
			var r = linear / angular;

			_odomPose.y += angular;
			_odomPose.z += r * (Math.Sin(_odomPose.y) - Math.Sin(headingOld));
			_odomPose.x += -r * (Math.Cos(_odomPose.y) - Math.Cos(headingOld));
			// Debug.Log("CalcOdom 1 = " + _odomPose.y + ", " + angular);
		}

		_odomPose.y.NormalizeAngle();
	}

	public bool Update(
		messages.Micom.Odometry odomMessage,
		in float angularVelocityLeft, in float angularVelocityRight,
		in float duration,
		SensorDevices.IMU imuSensor)
	{
		if (odomMessage == null)
		{
			return false;
		}

		if (!float.IsNaN(angularVelocityLeft) && !float.IsNaN(angularVelocityRight))
		{
			odomMessage.AngularVelocity.Left = Unity2SDF.Direction.Curve(angularVelocityLeft);
			odomMessage.AngularVelocity.Right = Unity2SDF.Direction.Curve(angularVelocityRight);
			odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * _wheelInfo.wheelRadius;
			odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * _wheelInfo.wheelRadius;
		}
		else
		{
			return false;
		}

		if (imuSensor != null)
		{
			var imuOrientation = imuSensor.GetOrientation();
			var imuYaw = imuOrientation.y;
			var deltaAngleImu = Mathf.DeltaAngle(_lastImuYaw, imuYaw);
			_lastImuYaw = imuYaw;

			var deltaThetaIMU = IsZero(deltaAngleImu) ? 0 : deltaAngleImu * Mathf.Deg2Rad;
			// Debug.Log("IMUE deltatheta = " + deltaThetaIMU);
			CalculateOdometry(angularVelocityLeft, angularVelocityRight, duration, deltaThetaIMU);
		}
		else
		{
			CalculateOdometry(angularVelocityLeft, angularVelocityRight, duration);
		}

		var odomPose = new Vector3((float)_odomPose.x, (float)_odomPose.y, (float)_odomPose.z);
		odomMessage.Pose.Set(Unity2SDF.Direction.Reverse(odomPose));

		var odomTransVel = Unity2SDF.Direction.Curve(_odomTranslationalVelocity);
		var odomAngularVel = Unity2SDF.Direction.Curve(_odomRotationalVelocity);

#if USE_ROLLINGMEAN_FOR_ODOM
		// rolling mean filtering
		rollingMeanOdomTransVelocity.Accumulate(odomTransVel);
		rollingMeanOdomTAngularVelocity.Accumulate(odomAngularVel);
		odomMessage.TwistLinear.X = rollingMeanOdomTransVelocity.Get();
		odomMessage.TwistAngular.Z = rollingMeanOdomTAngularVelocity.Get();
#else
		odomMessage.TwistLinear.X = odomTransVel;
		odomMessage.TwistAngular.Z = odomAngularVel;
#endif
		// Debug.LogFormat("odom Vel: {0:F6}, {1:F6}", odomMessage.TwistLinear.X, odomMessage.TwistAngular.Z);
		// Debug.LogFormat("Odom angular: {0:F6}, {1:F6}", odomMessage.AngularVelocity.Left, odomMessage.AngularVelocity.Right);
		return true;
	}
}