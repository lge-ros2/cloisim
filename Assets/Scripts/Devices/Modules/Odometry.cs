/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public partial class Odometry
{
	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private MotorControl motorControl = null;
	private WheelInfo wheelInfo;

	private float _lastImuYaw = 0f;
	private Vector3 _odomPose = Vector3.zero;
	private float _odomTranslationalVelocity = 0;
	private float _odomRotationalVelocity = 0;


	private const int RollingMeanWindowSize = 10;
	private RollingMean rollingMeanOdomTransVelocity = new RollingMean(RollingMeanWindowSize);
	private RollingMean rollingMeanOdomTAngularVelocity = new RollingMean(RollingMeanWindowSize);


	public float WheelSeparation => this.wheelInfo.wheelSeparation;
	public float InverseWheelRadius => this.wheelInfo.inversedWheelRadius;

	public Odometry(in float radius, in float separation)
	{
		this.wheelInfo = new WheelInfo(radius, separation);
	}

	public void SetMotorControl(in MotorControl motorControl)
	{
		this.motorControl = motorControl;
	}

	public void Reset()
	{
		_odomTranslationalVelocity = 0;
		_odomRotationalVelocity = 0;
		_odomPose.Set(0, 0, 0);
		_lastImuYaw = 0.0f;

		rollingMeanOdomTransVelocity.Reset();
		rollingMeanOdomTAngularVelocity.Reset();
	}

	/// <summary>Calculate odometry on this robot</summary>
	/// <remarks>rad per second for `angularVelocity`</remarks>
	/// <remarks>rad per second for `theta`</remarks>
	void CalculateOdometry(in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float duration, in float deltaTheta)
	{
		// circumference of wheel [rad] per step time.
		var wheelCircumLeft = angularVelocityLeftWheel * duration;
		var wheelCircumRight = angularVelocityRightWheel * duration;

		// Debug.LogFormat("theta:{0} lastTheta:{1} deltaTheta:{2}", theta, _lastTheta, deltaTheta);

		// compute odometric pose
		var poseLinear = wheelInfo.wheelRadius * (wheelCircumLeft + wheelCircumRight) * 0.5f;
		poseLinear = Mathf.Approximately(poseLinear, Quaternion.kEpsilon) ? 0 : poseLinear;

		var halfDeltaTheta = deltaTheta * 0.5f;
		var poseZ = poseLinear * Mathf.Cos(_odomPose.y + halfDeltaTheta);
		poseZ = Mathf.Approximately(poseZ, Quaternion.kEpsilon) ? 0 : poseZ;

		var poseX = poseLinear * Mathf.Sin(_odomPose.y + halfDeltaTheta);
		poseX = Mathf.Approximately(poseX, Quaternion.kEpsilon) ? 0 : poseX;

		_odomPose.z += poseZ;
		_odomPose.x += poseX;
		_odomPose.y += deltaTheta;

		// compute odometric instantaneouse velocity
		var divideDuration = 1f / duration;
		_odomTranslationalVelocity = poseLinear * divideDuration; // translational velocity [m/s]
		_odomRotationalVelocity = deltaTheta * divideDuration; // rotational velocity [rad/s]
	}

	/// <summary>Calculate odometry on this robot</summary>
	/// <remarks>rad per second for `angularVelocity`</remarks>
	void CalculateOdometry(in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float duration)
	{
		var linearVelocityLeftWheel = angularVelocityLeftWheel * wheelInfo.wheelRadius;
		var linearVelocityRightWheel = angularVelocityRightWheel * wheelInfo.wheelRadius;

		var sumLeftRight = linearVelocityLeftWheel + linearVelocityRightWheel;
		var diffRightLeft = linearVelocityRightWheel - linearVelocityLeftWheel;

		_odomTranslationalVelocity = (Mathf.Approximately(sumLeftRight, Quaternion.kEpsilon) ? 0 : sumLeftRight) * 0.5f;
		_odomRotationalVelocity = (Mathf.Approximately(diffRightLeft, Quaternion.kEpsilon) ? 0 : diffRightLeft) * wheelInfo.inversedWheelSeparation;

		var linear = _odomTranslationalVelocity * duration;
		var angular = _odomRotationalVelocity * duration;

		// Acculumate odometry:
		if (Mathf.Abs(angular) < Quaternion.kEpsilon) // RungeKutta2
		{
			var direction = _odomPose.y + angular * 0.5f;

			// Runge-Kutta 2nd order integration:
			_odomPose.z += linear * Mathf.Cos(direction);
			_odomPose.x += linear * Mathf.Sin(direction);
			_odomPose.y += angular;
		}
		else
		{
			// Exact integration (should solve problems when angular is zero):
			var heading_old = _odomPose.y;
			var r = linear / angular;

			_odomPose.y += angular;
			_odomPose.z += r * (Mathf.Sin(_odomPose.y) - Mathf.Sin(heading_old));
			_odomPose.x += -r * (Mathf.Cos(_odomPose.y) - Mathf.Cos(heading_old));
		}
	}

	public bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		if (odomMessage == null || motorControl == null)
		{
			return false;
		}

		if (motorControl.GetCurrentVelocity(MotorControl.WheelLocation.LEFT, out var angularVelocityLeft) &&
			motorControl.GetCurrentVelocity(MotorControl.WheelLocation.RIGHT, out var angularVelocityRight))
		{
			odomMessage.AngularVelocity.Left = DeviceHelper.Convert.CurveOrientation(angularVelocityLeft);
			odomMessage.AngularVelocity.Right = DeviceHelper.Convert.CurveOrientation(angularVelocityRight);
			odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * wheelInfo.wheelRadius;
			odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * wheelInfo.wheelRadius;
		}
		else
		{
			return false;
		}

		if (imuSensor != null)
		{
			var imuOrientation = imuSensor.GetOrientation();
			var yaw = imuOrientation.y * Mathf.Deg2Rad;
			var deltaThetaImu = yaw - _lastImuYaw;
			_lastImuYaw = yaw;

			if (deltaThetaImu > _PI)
			{
				deltaThetaImu -= _2_PI;
			}
			else if (deltaThetaImu < -_PI)
			{
				deltaThetaImu += _2_PI;
			}

			deltaThetaImu = Mathf.Approximately(deltaThetaImu, Quaternion.kEpsilon) ? 0 : deltaThetaImu;

			CalculateOdometry(angularVelocityLeft, angularVelocityRight, duration, deltaThetaImu);
		}
		else
		{
			CalculateOdometry(angularVelocityLeft, angularVelocityRight, duration);
		}

		DeviceHelper.SetVector3d(odomMessage.Pose, DeviceHelper.Convert.Reverse(_odomPose));


		// rolling mean filtering
		var odomTransVel = DeviceHelper.Convert.CurveOrientation(_odomTranslationalVelocity);
		rollingMeanOdomTransVelocity.Accumulate(odomTransVel);

		var odomAngularVel = DeviceHelper.Convert.CurveOrientation(_odomRotationalVelocity);
		rollingMeanOdomTAngularVelocity.Accumulate(odomAngularVel);

		odomMessage.TwistLinear.X = rollingMeanOdomTransVelocity.Get();
		odomMessage.TwistAngular.Z = rollingMeanOdomTAngularVelocity.Get();

		// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft, angularVelocityRight);
		// Debug.LogFormat("Odom: {0}, {1}", odomMessage.AngularVelocity.Left, odomMessage.AngularVelocity.Right);
		return true;
	}
}