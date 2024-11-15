/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
//  #define CALCULATE_ANGULAR_BY_YAW

using System;

namespace SelfBalanceControl
{
	class Kinematics
	{
		private WheelInfo _wheelInfo;
		private Vector2d _odomPose = Vector2d.zero;
		private Vector3d _rotation = Vector3d.zero; // roll, pitch, yaw

		private double _odomTranslationalVelocity = 0;
		private double _odomRotationalVelocity = 0;

		private double _s = 0; // displacement,s
		private double _sRef = 0;
		private double _previousLinearVelocity = 0;
		private double _previousPitch = double.NaN;

#if CALCULATE_ANGULAR_BY_YAW
		private double _previousYaw = 0;
#endif
		public WheelInfo WheelInfo => _wheelInfo;

		public double OdomTranslationalVelocity => Unity2SDF.Direction.Curve(_odomTranslationalVelocity);
		public double OdomRotationalVelocity => Unity2SDF.Direction.Curve(_odomRotationalVelocity);

		public Kinematics(in float radius, in float separation)
		{
			this._wheelInfo = new WheelInfo(radius, separation);
		}

		public void Reset(in double wheelVelocityLeft, in double wheelVelocityRight)
		{
			_previousLinearVelocity = (this._wheelInfo.halfWheelRadius) * (wheelVelocityLeft + wheelVelocityRight);
			_previousPitch = double.NaN;
			_s = _sRef = 0;
			_odomPose.Set(0, 0);
		}

		public VectorXd ComputeStates(
			in double wheelVelocityLeft, in double wheelVelocityRight,
			in double yaw, in double pitch, in double roll,
			in double deltaTime)
		{
			var halfWheelRadius = this._wheelInfo.halfWheelRadius;

			var wheelVelocitySum = wheelVelocityRight + wheelVelocityLeft;
			var wheelVelocityDiff = wheelVelocityRight - wheelVelocityLeft;

			var linearVelocity = halfWheelRadius * wheelVelocitySum;
#if CALCULATE_ANGULAR_BY_YAW
			var angularVelocity = (yaw - _previousYaw) / deltaTime;
			_previousYaw = yaw;
#else
			var angularVelocity = halfWheelRadius * wheelVelocityDiff * this._wheelInfo.inversedWheelSeparation;
			// this._wheelInfo.wheelRadius * halfInverseWheelSeparation * wheelVelocityDiff;
#endif
			// UnityEngine.Debug.Log($"wheelVelocity R/L: {wheelVelocityRight}/{wheelVelocityLeft}");

			_odomTranslationalVelocity = linearVelocity;
			_odomRotationalVelocity = angularVelocity;

			var pitchDot = (double.IsNaN(_previousPitch)) ? 0 : ((pitch - _previousPitch) / deltaTime);
			_s += 0.5 * (linearVelocity + _previousLinearVelocity) * deltaTime;

			// UnityEngine.Debug.Log($"{_previousLinearVelocity}->{linearVelocity} {_s}");

			// v, w, pitch_dot, pitch, s
			var states = new VectorXd(new double[]
				{
					linearVelocity,
					angularVelocity,
					pitchDot,
					pitch,
					-_s
				});

			// UnityEngine.Debug.Log($"ComputeStates: {_previousPitch} -> {pitch}: pitchDot={pitchDot}");
			_previousLinearVelocity = linearVelocity;
			_previousPitch = pitch;

			// calculate odom
			var ssum = wheelVelocitySum * halfWheelRadius * deltaTime;
			var sdiff = wheelVelocityDiff * halfWheelRadius * deltaTime;

			var halfInverseWheelSeparation = this._wheelInfo.inversedWheelSeparation * 0.5f;
			var deltaX = ssum * Math.Cos(yaw + sdiff / halfInverseWheelSeparation);
			var deltaY = ssum * Math.Sin(yaw + sdiff / halfInverseWheelSeparation);

			_odomPose.x += deltaX;
			_odomPose.y += deltaY;

			_rotation.x = roll;
			_rotation.y = pitch;
			_rotation.z = yaw;

			// Debug.Log($"wipStates: {wipStates}");
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