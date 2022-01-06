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
		private messages.Imu imu = null;

		private Vector3 imuInitialRotation = Vector3.zero;
		private Vector3 lastImuInitialRotation = Vector3.zero;
		private Quaternion imuOrientation = Quaternion.identity;
		private Vector3 imuAngularVelocity = Vector3.zero;
		private Vector3 imuLinearAcceleration = Vector3.zero;

		private Vector3 previousImuPosition = Vector3.zero;
		private Vector3 previousImuRotation = Vector3.zero;
		private Vector3 previousLinearVelocity = Vector3.zero;

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
			imuInitialRotation = transform.rotation.eulerAngles;
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			previousImuRotation = Vector3.zero;
		}

		protected override void InitializeMessages()
		{
			imu = new messages.Imu();
			imu.Stamp = new messages.Time();
			imu.Orientation = new messages.Quaternion();
			imu.AngularVelocity = new messages.Vector3d();
			imu.LinearAcceleration = new messages.Vector3d();
		}

		protected override void SetupMessages()
		{
			imu.EntityName = DeviceName;
		}

		void FixedUpdate()
		{
			// Caculate orientation and acceleration
			lastImuInitialRotation = transform.rotation.eulerAngles;
			var imuRotation = lastImuInitialRotation - imuInitialRotation;
			imuOrientation = Quaternion.Euler(imuRotation.x, imuRotation.y, imuRotation.z);

			imuAngularVelocity.x = Mathf.DeltaAngle(imuRotation.x, previousImuRotation.x) / Time.fixedDeltaTime;
			imuAngularVelocity.y = Mathf.DeltaAngle(imuRotation.y, previousImuRotation.y) / Time.fixedDeltaTime;
			imuAngularVelocity.z = Mathf.DeltaAngle(imuRotation.z, previousImuRotation.z) / Time.fixedDeltaTime;

			// apply noise
			if (angular_velocity_noises["x"] != null)
			{
				angular_velocity_noises["x"].Apply<float>(ref imuAngularVelocity.x, Time.fixedDeltaTime);
			}

			if (angular_velocity_noises["y"] != null)
			{
				angular_velocity_noises["y"].Apply<float>(ref imuAngularVelocity.y, Time.fixedDeltaTime);
			}

			if (angular_velocity_noises["z"] != null)
			{
				angular_velocity_noises["z"].Apply<float>(ref imuAngularVelocity.z, Time.fixedDeltaTime);
			}

			var currentPosition = transform.position;
			var currentLinearVelocity = (currentPosition - previousImuPosition) / Time.fixedDeltaTime;
			imuLinearAcceleration = (currentLinearVelocity - previousLinearVelocity) / Time.fixedDeltaTime;
			imuLinearAcceleration.y += (-Physics.gravity.y);

			// apply noise
			if (linear_acceleration_noises["x"] != null)
			{
				linear_acceleration_noises["x"].Apply<float>(ref imuLinearAcceleration.x, Time.fixedDeltaTime);
			}

			if (linear_acceleration_noises["y"] != null)
			{
				linear_acceleration_noises["y"].Apply<float>(ref imuLinearAcceleration.y, Time.fixedDeltaTime);
			}

			if (linear_acceleration_noises["z"] != null)
			{
				linear_acceleration_noises["z"].Apply<float>(ref imuLinearAcceleration.z, Time.fixedDeltaTime);
			}

			previousImuRotation = imuRotation;
			previousImuPosition = currentPosition;
			previousLinearVelocity = currentLinearVelocity;
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetQuaternion(imu.Orientation, imuOrientation);
			DeviceHelper.SetVector3d(imu.AngularVelocity, imuAngularVelocity * Mathf.Deg2Rad);
			DeviceHelper.SetVector3d(imu.LinearAcceleration, imuLinearAcceleration);
			DeviceHelper.SetCurrentTime(imu.Stamp);
			PushDeviceMessage<messages.Imu>(imu);
		}

		public messages.Imu GetImuMessage()
		{
			return imu;
		}

		public Vector3 GetOrientation()
		{
			return imuOrientation.eulerAngles;
		}

		public Vector3 GetAngularVelocity()
		{
			return imuAngularVelocity;
		}

		public Vector3 GetLinearAcceleration()
		{
			return imuLinearAcceleration;
		}
	}
}