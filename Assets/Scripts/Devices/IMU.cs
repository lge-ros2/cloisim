/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class IMU : Device
	{
		private messages.Imu _imu = null;

		private Quaternion _imuInitialRotation = Quaternion.identity;
		private Quaternion _lastImuInitialRotation = Quaternion.identity;
		private Quaternion _imuOrientation = Quaternion.identity;
		private Vector3 _imuAngularVelocity = Vector3.zero;
		private Vector3 _imuLinearAcceleration = Vector3.zero;

		private Vector3 _previousImuPosition = Vector3.zero;
		private Quaternion _previousImuRotation = Quaternion.identity;
		private Vector3 _previousLinearVelocity = Vector3.zero;

		public Dictionary<string, Noise> angular_velocity_noises = new Dictionary<string, Noise>()
		{
			{"x", null},
			{"y", null},
			{"z", null}
		};

		public Dictionary<string, Noise> linear_acceleration_noises = new Dictionary<string, Noise>()
		{
			{"x", null},
			{"y", null},
			{"z", null}
		};

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
			Reset();
		}

		protected override void OnStart()
		{
			_imuInitialRotation = transform.rotation;
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			_previousImuRotation = Quaternion.identity;
		}

		protected override void InitializeMessages()
		{
			_imu = new messages.Imu();
			_imu.Stamp = new messages.Time();
			_imu.Orientation = new messages.Quaternion();
			_imu.AngularVelocity = new messages.Vector3d();
			_imu.LinearAcceleration = new messages.Vector3d();
		}

		protected override void SetupMessages()
		{
			_imu.EntityName = DeviceName;
		}

		private void ApplyNoises(in float deltaTime)
		{
			if (angular_velocity_noises["x"] != null)
			{
				angular_velocity_noises["x"].Apply<float>(ref _imuAngularVelocity.x, deltaTime);
			}

			if (angular_velocity_noises["y"] != null)
			{
				angular_velocity_noises["y"].Apply<float>(ref _imuAngularVelocity.y, deltaTime);
			}

			if (angular_velocity_noises["z"] != null)
			{
				angular_velocity_noises["z"].Apply<float>(ref _imuAngularVelocity.z, deltaTime);
			}

			if (linear_acceleration_noises["x"] != null)
			{
				linear_acceleration_noises["x"].Apply<float>(ref _imuLinearAcceleration.x, deltaTime);
			}

			if (linear_acceleration_noises["y"] != null)
			{
				linear_acceleration_noises["y"].Apply<float>(ref _imuLinearAcceleration.y, deltaTime);
			}

			if (linear_acceleration_noises["z"] != null)
			{
				linear_acceleration_noises["z"].Apply<float>(ref _imuLinearAcceleration.z, deltaTime);
			}
		}

		void FixedUpdate()
		{
			var currentPosition = transform.position;
			_lastImuInitialRotation = transform.rotation;

			// Caculate orientation and acceleration
			// Rotation from A to B : B * Quaternion.Inverse(A);
			_imuOrientation = _lastImuInitialRotation * Quaternion.Inverse(_imuInitialRotation);

			var angleDiff = _imuOrientation * Quaternion.Inverse(_previousImuRotation);
			_imuAngularVelocity = angleDiff.eulerAngles;
			_imuAngularVelocity /= Time.fixedDeltaTime;

			var currentLinearVelocity = (currentPosition - _previousImuPosition) / Time.fixedDeltaTime;
			_imuLinearAcceleration = (currentLinearVelocity - _previousLinearVelocity) / Time.fixedDeltaTime;
			_imuLinearAcceleration.y += (-Physics.gravity.y);

			ApplyNoises(Time.fixedDeltaTime);

			_previousImuRotation = _imuOrientation;
			_previousImuPosition = currentPosition;
			_previousLinearVelocity = currentLinearVelocity;
		}

		protected override void GenerateMessage()
		{
			_imu.Orientation.Set(_imuOrientation);
			_imu.AngularVelocity.Set(_imuAngularVelocity * Mathf.Deg2Rad);
			_imu.LinearAcceleration.Set(_imuLinearAcceleration);
			_imu.Stamp.SetCurrentTime();
			PushDeviceMessage<messages.Imu>(_imu);
		}

		public messages.Imu GetImuMessage()
		{
			return _imu;
		}

		public Vector3 GetOrientation()
		{
			return MathUtil.Angle.GetEuler(_imuOrientation);
		}

		public Vector3 GetAngularVelocity()
		{
			return _imuAngularVelocity;
		}

		public Vector3 GetLinearAcceleration()
		{
			return _imuLinearAcceleration;
		}
	}
}