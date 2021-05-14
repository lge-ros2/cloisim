/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class MicomSensor : Device
{
	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private float lastImuYaw = 0f;
	private Vector3 _odomPose = Vector3.zero;
	private Vector2 _odomVelocity = Vector2.zero;

	protected override void OnReset()
	{
		if (imuSensor != null)
		{
			imuSensor.Reset();
		}
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
		var poseLinear = wheelRadius * (wheelCircumLeft + wheelCircumRight) * 0.5f;
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

	public bool UpdateOdom(in float duration)
	{
		if (micomSensorData != null)
		{
			var odom = micomSensorData.Odom;
			if ((odom != null))
			{
				if (!_motors.TryGetValue(wheelNameLeft, out var motorLeft))
				{
					Debug.Log("cannot find motor object: " + wheelNameLeft);
					return false;
				}

				if (!_motors.TryGetValue(wheelNameRight, out var motorRight))
				{
					Debug.Log("cannot find motor object: " + wheelNameRight);
					return false;
				}

				if (motorLeft == null || motorRight == null)
				{
					Debug.Log("cannot find motor object");
					return false;
				}

				var angularVelocityLeft = motorLeft.GetCurrentVelocity();
				var angularVelocityRight = motorRight.GetCurrentVelocity();

				// Set reversed value due to different direction
				// Left-handed -> Right-handed direction of rotation
				odom.AngularVelocity.Left = -angularVelocityLeft * Mathf.Deg2Rad;
				odom.AngularVelocity.Right = -angularVelocityRight * Mathf.Deg2Rad;
				odom.LinearVelocity.Left = odom.AngularVelocity.Left * wheelRadius;
				odom.LinearVelocity.Right = odom.AngularVelocity.Right * wheelRadius;

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
					var yaw = ((float)(odom.LinearVelocity.Right - odom.LinearVelocity.Left) / wheelTread);
					deltaTheta = yaw * duration;
				}

				CalculateOdometry(duration, (float)odom.AngularVelocity.Left, (float)odom.AngularVelocity.Right, deltaTheta);

				// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
				odom.Pose.X = _odomPose.x;
				odom.Pose.Y = -_odomPose.y;
				odom.Pose.Z = -_odomPose.z;

				odom.TwistLinear.X = _odomVelocity.x;

				// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
				odom.TwistAngular.Z = -_odomVelocity.y;

				motorLeft.Feedback.SetRotatingVelocity(_odomVelocity.y);
				motorRight.Feedback.SetRotatingVelocity(_odomVelocity.y);
				// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft * Mathf.Deg2Rad, angularVelocityRight * Mathf.Deg2Rad);
				// Debug.LogFormat("Odom: {0}, {1}", odom.AngularVelocity.Left, odom.AngularVelocity.Right);

				return true;
			}
		}

		return false;
	}
}