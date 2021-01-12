/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public class IMU : Device
	{
		private messages.Imu imu = null;

		// <noise_angular_velocity_x>
		// <noise_angular_velocity_y>
		// <noise_angular_velocity_z>
		// <noise_linear_acceleration_x>
		// <noise_linear_acceleration_y>
		// <noise_linear_acceleration_z>

		private Vector3 imuInitialRotation = Vector3.zero;
		private Quaternion imuOrientation = Quaternion.identity;
		private Vector3 imuAngularVelocity = Vector3.zero;
		private Vector3 imuLinearAcceleration = Vector3.zero;

		private Vector3 previousImuPosition = Vector3.zero;
		private Vector3 previousImuRotation = Vector3.zero;
		private Vector3 previousLinearVelocity = Vector3.zero;

		public float samplingRate = 100f;
		public float samplingPeriod = 0;
		public float timeElapsed = 0;

		protected override void OnAwake()
		{
			deviceName = name;
			Reset();
		}

		protected override void OnStart()
		{
			samplingPeriod = 1/samplingRate;
		}

		public void Reset()
		{
			// Debug.Log("IMU Reset");
			imuInitialRotation = transform.rotation.eulerAngles;
			previousImuPosition = transform.position;
			previousImuRotation = Vector3.zero;
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
		}

		protected override void InitializeMessages()
		{
			imu = new messages.Imu();
			imu.EntityName = deviceName;
			imu.Stamp = new messages.Time();
			imu.Orientation = new messages.Quaternion();
			imu.AngularVelocity = new messages.Vector3d();
			imu.LinearAcceleration = new messages.Vector3d();
		}

		void FixedUpdate()
		{
			// Caculate orientation and acceleration
			var imuRotation = transform.rotation.eulerAngles - imuInitialRotation;
			imuOrientation = Quaternion.Euler(imuRotation.x, imuRotation.y, imuRotation.z);

			imuAngularVelocity.x = Mathf.DeltaAngle(imuRotation.x, previousImuRotation.x) / Time.fixedDeltaTime;
			imuAngularVelocity.y = Mathf.DeltaAngle(imuRotation.y, previousImuRotation.y) / Time.fixedDeltaTime;
			imuAngularVelocity.z = Mathf.DeltaAngle(imuRotation.z, previousImuRotation.z) / Time.fixedDeltaTime;

			var currentPosition = transform.position;
			var currentLinearVelocity = (currentPosition - previousImuPosition) / Time.fixedDeltaTime;
			imuLinearAcceleration = (currentLinearVelocity - previousLinearVelocity) / Time.fixedDeltaTime;
			imuLinearAcceleration.y += (-Physics.gravity.y);

			previousImuRotation = imuRotation;
			previousImuPosition = currentPosition;
			previousLinearVelocity = currentLinearVelocity;
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);
			while (true)
			{
				GenerateMessage();
				yield return waitForSeconds;
			}
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetQuaternion(imu.Orientation, imuOrientation);
			DeviceHelper.SetVector3d(imu.AngularVelocity, imuAngularVelocity * Mathf.Deg2Rad);
			DeviceHelper.SetVector3d(imu.LinearAcceleration, imuLinearAcceleration);
			DeviceHelper.SetCurrentTime(imu.Stamp);
			PushData<messages.Imu>(imu);
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