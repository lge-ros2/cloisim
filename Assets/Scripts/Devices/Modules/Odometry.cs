/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public class Odometry
{
	public struct WheelInfo
	{
		public float wheelRadius; // considering contact offset
		public float wheelSeparation; // wheel separation
		public float inversedWheelRadius; // for computational performance
		public float inversedWheelSeparation;  // for computational performance

		public WheelInfo(in float radius = 0.1f, in float separation = 0)
		{
			this.wheelRadius = radius;
			this.wheelSeparation = separation;
			this.inversedWheelRadius = 1.0f / wheelRadius;
			this.inversedWheelSeparation = 1.0f / wheelSeparation;
		}
	}

	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private MotorControl motorControl = null;
	private WheelInfo wheelInfo;

	private float lastImuYaw = 0f;
	private float lastYaw = 0f;
	private Vector3 _odomPose = Vector3.zero;
	private float odomTranslationalVelocity = 0;
	private float odomRotationalVelocity = 0;

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
		odomTranslationalVelocity = 0;
		odomRotationalVelocity = 0;
		_odomPose.Set(0, 0, 0);
		lastImuYaw = 0.0f;
	}

	/// <summary>Calculate odometry on this robot</summary>
	/// <remarks>rad per second for `theta`</remarks>
	void CalculateOdometry(in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float deltaTheta, in float duration)
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

		// Debug.LogFormat("({0}, {1}, {2}) = {3} {4} {5}, L: {6}, R: {7}, {8}", _odomPose.z, _odomPose.x, _odomPose.y, poseLinear, Mathf.Sin(_odomPose.y + halfDeltaTheta), Mathf.Cos(_odomPose.y + halfDeltaTheta), wheelCircumLeft, wheelCircumRight, halfDeltaTheta)

		// compute odometric instantaneouse velocity
		var divideDuration = 1f / duration;
		odomTranslationalVelocity = poseLinear * divideDuration; // translational velocity [m/s]
		odomRotationalVelocity = deltaTheta * divideDuration; // rotational velocity [rad/s]
	}

	void CalculateOdometry(in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float duration)
	{
		var linearVelocityLeftWheel = angularVelocityLeftWheel * wheelInfo.wheelRadius;
		var linearVelocityRightWheel = angularVelocityRightWheel * wheelInfo.wheelRadius;

		odomTranslationalVelocity = (linearVelocityLeftWheel + linearVelocityRightWheel) * 0.5f;
		odomRotationalVelocity = (linearVelocityRightWheel - linearVelocityLeftWheel) * wheelInfo.inversedWheelSeparation;

		var linear = odomTranslationalVelocity * duration;
		var angular = odomRotationalVelocity * duration;

		// Acculumate odometry:
		if (Mathf.Abs(angular) < Quaternion.kEpsilon) // RungeKutta2
		{
			var direction = _odomPose.y + angular * 0.5f;

			// Runge-Kutta 2nd order integration:
			_odomPose.z += linear * Mathf.Cos(direction);
			_odomPose.x += linear * Mathf.Sin(direction);
			_odomPose.y += angular;
			// Debug.LogFormat("RungeKutta2: {0:F4} {1:F4} {2:F4} {3:F4}", _odomPose.x, _odomPose.z, _odomPose.y, direction);
		}
		else
		{
			// Exact integration (should solve problems when angular is zero):
			var heading_old = _odomPose.y;
			var r = linear / angular;

			_odomPose.y += angular;
			_odomPose.z += r * (Mathf.Sin(_odomPose.y) - Mathf.Sin(heading_old));
			_odomPose.x += -r * (Mathf.Cos(_odomPose.y) - Mathf.Cos(heading_old));
			// Debug.LogFormat("CalculateOdometry: {0:F4} {1:F4} {2:F4} {3:F4}->{4:F4}", _odomPose.x, _odomPose.z, _odomPose.y, heading_old, _odomPose.y);
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
			var deltaThetaImu = yaw - lastImuYaw;
			lastImuYaw = yaw;

			if (deltaThetaImu > _PI)
			{
				deltaThetaImu -= _2_PI;
			}
			else if (deltaThetaImu < -_PI)
			{
				deltaThetaImu += _2_PI;
			}

			deltaThetaImu = Mathf.Approximately(deltaThetaImu, Quaternion.kEpsilon) ? 0 : deltaThetaImu;

			CalculateOdometry(angularVelocityLeft, angularVelocityRight, deltaThetaImu, duration);
		}
		else
		{
			CalculateOdometry(angularVelocityLeft, angularVelocityRight, duration);
		}

		DeviceHelper.SetVector3d(odomMessage.Pose, DeviceHelper.Convert.Reverse(_odomPose));

		odomMessage.TwistLinear.X = DeviceHelper.Convert.CurveOrientation(odomTranslationalVelocity);
		odomMessage.TwistAngular.Z = DeviceHelper.Convert.CurveOrientation(odomRotationalVelocity);

		motorControl.UpdateCurrentMotorFeedback(odomRotationalVelocity);
		// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft, angularVelocityRight);
		// Debug.LogFormat("Odom: {0}, {1}", odomMessage.AngularVelocity.Left, odomMessage.AngularVelocity.Right);
		return true;
	}
}