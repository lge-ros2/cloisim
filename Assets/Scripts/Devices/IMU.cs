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

		private Vector3 _imuInitialRotation = Vector3.zero;
		private Vector3 _lastImuInitialRotation = Vector3.zero;
		private Quaternion _imuOrientation = Quaternion.identity;
		private Vector3 _imuAngularVelocity = Vector3.zero;
		private Vector3 _imuLinearAcceleration = Vector3.zero;

		private Vector3 _previousImuPosition = Vector3.zero;
		private Vector3 _previousImuRotation = Vector3.zero;
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
			_imuInitialRotation = transform.rotation.eulerAngles;
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			_previousImuRotation = Vector3.zero;
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
			// Caculate orientation and acceleration
			_lastImuInitialRotation = transform.rotation.eulerAngles;
			var imuRotation = _lastImuInitialRotation - _imuInitialRotation;
			_imuOrientation = Quaternion.Euler(imuRotation.x, imuRotation.y, imuRotation.z);

			_imuAngularVelocity.x = Mathf.DeltaAngle(imuRotation.x, _previousImuRotation.x) / Time.fixedDeltaTime;
			_imuAngularVelocity.y = Mathf.DeltaAngle(imuRotation.y, _previousImuRotation.y) / Time.fixedDeltaTime;
			_imuAngularVelocity.z = Mathf.DeltaAngle(imuRotation.z, _previousImuRotation.z) / Time.fixedDeltaTime;

			var currentPosition = transform.position;
			var currentLinearVelocity = (currentPosition - _previousImuPosition) / Time.fixedDeltaTime;
			_imuLinearAcceleration = (currentLinearVelocity - _previousLinearVelocity) / Time.fixedDeltaTime;
			_imuLinearAcceleration.y += (-Physics.gravity.y);

			ApplyNoises(Time.fixedDeltaTime);

			_previousImuRotation = imuRotation;
			_previousImuPosition = currentPosition;
			_previousLinearVelocity = currentLinearVelocity;
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetQuaternion(_imu.Orientation, _imuOrientation);
			DeviceHelper.SetVector3d(_imu.AngularVelocity, _imuAngularVelocity * Mathf.Deg2Rad);
			DeviceHelper.SetVector3d(_imu.LinearAcceleration, _imuLinearAcceleration);
			_imu.Stamp.SetCurrentTime();
			PushDeviceMessage<messages.Imu>(_imu);
		}

		public messages.Imu GetImuMessage()
		{
			return _imu;
		}

		public Vector3 GetOrientation()
		{
			return _imuOrientation.eulerAngles;
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