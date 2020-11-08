/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

public partial class MicomSensor : Device
{
	private const float _PI = Mathf.PI;
	private const float _2_PI = _PI * 2.0f;

	private float _lastTheta = 0.0f;
	private Vector3 _odomPose = Vector3.zero;
	private Vector2 _odomVelocity = Vector2.zero;

	public void Reset()
	{
		_odomPose.Set(0, 0, 0);
		_odomVelocity.Set(0, 0);
	}

	private void CalculateOdometry(in float duration, in float angularVelocityLeftWheel, in float angularVelocityRightWheel, in float theta)
	{
		// circumference of wheel [rad] per step time.
		var wheelCircumLeft = angularVelocityLeftWheel * duration;
		var wheelCircumRight = angularVelocityRightWheel * duration;

		var deltaTheta = theta - _lastTheta;

		if (deltaTheta > _PI)
		{
			deltaTheta -= _2_PI;
		}
		else if (deltaTheta < -_PI)
		{
			deltaTheta += _2_PI;
		}

		// Debug.LogFormat("theta:{0} lastTheta:{1} deltaTheta:{2}", theta, _lastTheta, deltaTheta);

		// compute odometric pose
		var poseLinear = wheelRadius * (wheelCircumLeft + wheelCircumRight) / 2.0f;
		_odomPose.x += poseLinear * Mathf.Cos(_odomPose.z + (deltaTheta / 2.0f));
		_odomPose.y += poseLinear * Mathf.Sin(_odomPose.z + (deltaTheta / 2.0f));
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
		_odomVelocity.y = -w; // Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)

		_lastTheta = theta;
	}
}