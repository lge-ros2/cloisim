/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public class Odometry
{
	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private MotorControl motorControl = null;
	private Micom.WheelInfo wheelInfo;

	private float lastImuYaw = 0f;
	private Vector3 _odomPose = Vector3.zero;
	private Vector2 _odomVelocity = Vector2.zero;

	public Odometry(Micom.WheelInfo wheelInfo)
	{
		this.wheelInfo = wheelInfo;
	}

	public void SetMotorControl(in MotorControl motorControl)
	{
		this.motorControl = motorControl;
	}

	public void Reset()
	{
		_odomVelocity.Set(0, 0);
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
		_odomPose.x += poseLinear * Mathf.Cos(_odomPose.z + halfDeltaTheta);
		_odomPose.y += poseLinear * Mathf.Sin(_odomPose.z + halfDeltaTheta);
		_odomPose.z += deltaTheta;

		if (_odomPose.z > _PI)
		{
			_odomPose.z -= _2_PI;
		}
		else if (_odomPose.z < -_PI)
		{
			_odomPose.z += _2_PI;
		}

		// compute odometric instantaneouse velocity
		var v = poseLinear / duration; // v = translational velocity [m/s]
		var w = deltaTheta / duration; // w = rotational velocity [rad/s]

		_odomVelocity.x = v;
		_odomVelocity.y = w;
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

		// Set reversed value due to different direction
		// Left-handed -> Right-handed direction of rotation
		odomMessage.AngularVelocity.Left = -angularVelocityLeft * Mathf.Deg2Rad;
		odomMessage.AngularVelocity.Right = -angularVelocityRight * Mathf.Deg2Rad;
		odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * wheelInfo.wheelRadius;
		odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * wheelInfo.wheelRadius;

		var deltaTheta = 0f;
		if (imuSensor != null)
		{
			var imuOrientation = imuSensor.GetOrientation();
			var yaw = imuOrientation.y * Mathf.Deg2Rad;
			deltaTheta = yaw - lastImuYaw;

			if (deltaTheta > _PI)
			{
				deltaTheta -= _2_PI;
			}
			else if (deltaTheta < -_PI)
			{
				deltaTheta += _2_PI;
			}

			lastImuYaw = yaw;
		}
		else
		{
			var yaw = ((float)(odomMessage.LinearVelocity.Right - odomMessage.LinearVelocity.Left) / wheelInfo.wheelTread);
			deltaTheta = yaw * duration;
		}

		CalculateOdometry(duration, (float)odomMessage.AngularVelocity.Left, (float)odomMessage.AngularVelocity.Right, deltaTheta);

		// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
		odomMessage.Pose.X = _odomPose.x;
		odomMessage.Pose.Y = -_odomPose.y;
		odomMessage.Pose.Z = -_odomPose.z;

		odomMessage.TwistLinear.X = _odomVelocity.x;

		// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
		odomMessage.TwistAngular.Z = -_odomVelocity.y;

		motorLeft.Feedback.SetRotatingVelocity(_odomVelocity.y);
		motorRight.Feedback.SetRotatingVelocity(_odomVelocity.y);
		// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft * Mathf.Deg2Rad, angularVelocityRight * Mathf.Deg2Rad);
		// Debug.LogFormat("Odom: {0}, {1}", odomMessage.AngularVelocity.Left, odomMessage.AngularVelocity.Right);

		return true;
	}
}