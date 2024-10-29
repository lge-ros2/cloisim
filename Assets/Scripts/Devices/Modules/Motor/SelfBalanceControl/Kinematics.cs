/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SelfBalanceControl
{
	class Kinematics
	{
		private WheelInfo _wheelInfo;
		private Vector2d _odomPose = Vector2d.zero;
		private Vector3d _rotation = Vector3d.zero; // roll, pitch, yaw

		private double _s = 0; // displacement,s
		private double _sRef = 0;
		private double _previousLinearVelocity = 0;
		private double _previousPitch = 0;

		public WheelInfo WheelInfo => _wheelInfo;

		public Kinematics(in float radius, in float separation)
		{
			this._wheelInfo = new WheelInfo(radius, separation);
		}

		public void Reset(in double wheelVelocityLeft, in double wheelVelocityRight, in double initPitch)
		{
			_previousLinearVelocity = (this._wheelInfo.wheelRadius * 0.5) * (wheelVelocityLeft + wheelVelocityRight);
			_previousPitch = initPitch;
			_s = _sRef = 0;
			_odomPose.Set(0, 0);
		}

		public VectorXd ComputeStates(
			in double wheelVelocityLeft, in double wheelVelocityRight,
			in double yaw, in double pitch, in double roll,
			in double deltaTime)
		{
			var halfWheelRadius = (this._wheelInfo.wheelRadius * 0.5);
			var inverseDoubleWheelSeparation = 1 / (2 * this._wheelInfo.wheelSeparation);

			var linearVelocity = halfWheelRadius * (wheelVelocityRight + wheelVelocityLeft);
			var w = this._wheelInfo.wheelRadius * inverseDoubleWheelSeparation * (wheelVelocityRight - wheelVelocityLeft);
			// UnityEngine.Debug.Log($"wheelVelocity R/L: {wheelVelocityRight}/{wheelVelocityLeft}");

			var pitchDot = (pitch - _previousPitch) / deltaTime;
			_s += 0.5 * (linearVelocity + _previousLinearVelocity) * deltaTime;

			// v, w, pitch_dot, pitch, s
			var states = new VectorXd(new double[]
				{
					linearVelocity,
					w,
					pitchDot,
					pitch,
					_s
				});

			// UnityEngine.Debug.Log($"ComputeStates: {_previousPitch} -> {pitch}");
			_previousLinearVelocity = linearVelocity;
			_previousPitch = pitch;

			// calculate odom
			var sl = wheelVelocityLeft * (halfWheelRadius) * deltaTime;
			var sr = wheelVelocityRight * (halfWheelRadius) * deltaTime;
			var ssum = sl + sr;
			var sdiff = sr - sl;

			var deltaX = (ssum) * Math.Cos(yaw + sdiff / inverseDoubleWheelSeparation);
			var deltaY = (ssum) * Math.Sin(yaw + sdiff / inverseDoubleWheelSeparation);

			_odomPose.x += deltaX;
			_odomPose.y += deltaY;

			_rotation.x = roll;
			_rotation.y = pitch;
			_rotation.z = yaw;

			return states;
		}

		public Vector2d GetOdom()
		{
			return _odomPose;
		}

		public Vector3d GetRotation()
		{
			return _rotation;
		}

		public VectorXd GetReferences(
			in double v, in double w,
			in double pitch, in double pitchDerivative,
			in double deltaTime)
		{
			_sRef += v * deltaTime;
			return new VectorXd(new double[]
				{
					v,
					w,
					pitchDerivative,
					pitch,
					_sRef
				});
		}
	}
}