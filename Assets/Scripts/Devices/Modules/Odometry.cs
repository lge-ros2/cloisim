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
		public float wheelRadius;
		public float wheelTread;
		public float inverseWheelRadius; // for computational performance

		public WheelInfo(in float radius = 0.1f, in float tread = 0)
		{
			this.wheelRadius = radius;
			this.wheelTread = tread;
			this.inverseWheelRadius = 1.0f / wheelRadius;
		}
	}

	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private MotorControl motorControl = null;
	private WheelInfo wheelInfo;

	private float lastImuYaw = 0f;
	private Vector3 _odomPose = Vector3.zero;
	private float odomTranslationalVelocity = 0;
	private float odomRotationalVelocity = 0;

	public float WheelTread => this.wheelInfo.wheelTread;
	public float InverseWheelRadius => this.wheelInfo.inverseWheelRadius;

	public Odometry(in float radius, in float tread)
	{
		this.wheelInfo = new WheelInfo(radius, tread);;
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
	private void CalculateOdometry(in float duration, in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float deltaTheta)
	{
		// circumference of wheel [rad] per step time.
		var wheelCircumLeft = angularVelocityLeftWheel * duration;
		var wheelCircumRight = angularVelocityRightWheel * duration;

		// Debug.LogFormat("theta:{0} lastTheta:{1} deltaTheta:{2}", theta, _lastTheta, deltaTheta);

		// compute odometric pose
		var poseLinear = wheelInfo.wheelRadius * (wheelCircumLeft + wheelCircumRight) * 0.5f;

		var halfDeltaTheta = deltaTheta * 0.5f;
		_odomPose.z += poseLinear * Mathf.Cos(_odomPose.y + halfDeltaTheta);
		_odomPose.x += poseLinear * Mathf.Sin(_odomPose.y + halfDeltaTheta);
		_odomPose.y += deltaTheta;

		// Debug.LogFormat("({0}, {1}, {2}) = {3} {4} {5}, L: {6}, R: {7}, {8}", _odomPose.z, _odomPose.x, _odomPose.y, poseLinear, Mathf.Sin(_odomPose.y + halfDeltaTheta), Mathf.Cos(_odomPose.y + halfDeltaTheta), wheelCircumLeft, wheelCircumRight, halfDeltaTheta)

		// compute odometric instantaneouse velocity
		var divideDuration = 1f / duration;
		odomTranslationalVelocity = poseLinear * divideDuration; // translational velocity [m/s]
		odomRotationalVelocity = deltaTheta * divideDuration; // rotational velocity [rad/s]
	}

	public bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		if (odomMessage == null || motorControl == null)
		{
			return false;
		}

		var motorLeft = motorControl.GetMotor(MotorControl.WheelLocation.LEFT);
		var motorRight = motorControl.GetMotor(MotorControl.WheelLocation.RIGHT);

		if (motorLeft == null || motorRight == null)
		{
			return false;
		}

		var angularVelocityLeft = motorLeft.GetCurrentVelocity();
		var angularVelocityRight = motorRight.GetCurrentVelocity();

		odomMessage.AngularVelocity.Left = DeviceHelper.Convert.CurveOrientation(angularVelocityLeft);
		odomMessage.AngularVelocity.Right = DeviceHelper.Convert.CurveOrientation(angularVelocityRight);
		odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * wheelInfo.wheelRadius;
		odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * wheelInfo.wheelRadius;

		var deltaTheta = 0f;
		if (imuSensor != null)
		{
			var imuOrientation = imuSensor.GetOrientation();
			var yaw = imuOrientation.y * Mathf.Deg2Rad;
			deltaTheta = yaw - lastImuYaw;
			lastImuYaw = yaw;
		}
		else
		{
			var yaw = (float)((angularVelocityRight - angularVelocityLeft) * wheelInfo.wheelRadius  / wheelInfo.wheelTread);
				// odomMessage.LinearVelocity.Right - odomMessage.LinearVelocity.Left) / wheelInfo.wheelTread);
			deltaTheta = yaw * duration;
		}

		if (deltaTheta > _PI)
		{
			deltaTheta -= _2_PI;
		}
		else if (deltaTheta < -_PI)
		{
			deltaTheta += _2_PI;
		}

		CalculateOdometry(duration, angularVelocityLeft, angularVelocityRight, deltaTheta);

		DeviceHelper.SetVector3d(odomMessage.Pose, DeviceHelper.Convert.Reverse(_odomPose));

		odomMessage.TwistLinear.X = odomTranslationalVelocity;
		odomMessage.TwistAngular.Z = DeviceHelper.Convert.CurveOrientation(odomRotationalVelocity);

		motorLeft.Feedback.SetRotatingVelocity(odomRotationalVelocity);
		motorRight.Feedback.SetRotatingVelocity(odomRotationalVelocity);
		// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft, angularVelocityRight);
		// Debug.LogFormat("Odom: {0}, {1}", odomMessage.AngularVelocity.Left, odomMessage.AngularVelocity.Right);

		return true;
	}
}