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
		private Quaternion _imuRotation = Quaternion.identity;

		private Vector3 _imuOrientation = Vector3.zero;
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
			Debug.Log("_imuInitialRotation=" + _imuInitialRotation);
			// _imuOrientation.x = _imuInitialRotation.eulerAngles.x;
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			_previousImuRotation = Quaternion.identity;
			
			_imuOrientation = Vector3.zero;
			// _imuOrientation.x = _imuRotation.eulerAngles.x;
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
		
		private float CalculatePitchFromForwardBaseAxis()
		{
			var rotatedBase = _imuRotation * Vector3.forward;

			// Step 2: Calculate the Euler Angles
			// Calculate the yaw angle (rotation around the Y-axis)
			// var yaw = Mathf.Atan2(rotatedBase.x, rotatedBase.z) * Mathf.Rad2Deg;

			// Calculate the pitch angle (rotation around the X-axis)
			var horizontalDistance = new Vector2(rotatedBase.x, rotatedBase.z).magnitude;  // Projected distance on XZ-plane
			var pitch = Mathf.Atan2(rotatedBase.y, horizontalDistance) * Mathf.Rad2Deg;

			var adjustedPitch = 
				(rotatedBase.x < 0 && rotatedBase.z < 0 &&
				 rotatedBase.y >= 0.1f && rotatedBase.y < 1.0f) ? (pitch - 180) : -pitch;
			
			// Debug.Log($"{rotatedBase.ToString("F10")}, pitch={pitch} -> {adjustedPitch}");

			// For roll, assuming this is relative to the base axis direction
			// var projectedZ = Vector3.ProjectOnPlane(rotatedBase, Vector3.forward);
			// var roll = Mathf.Atan2(projectedZ.y, projectedZ.x) * Mathf.Rad2Deg;

			// return new Vector3(newPitch, yaw, roll);
			return adjustedPitch;
		}

		void FixedUpdate()
		{
			var currentPosition = transform.position;

			// Caculate orientation and acceleration
			// Rotation from A to B : B * Quaternion.Inverse(A);
			_imuRotation = transform.rotation * Quaternion.Inverse(_imuInitialRotation);

			var angularDisplacement = _imuRotation * Quaternion.Inverse(_previousImuRotation);
			_imuAngularVelocity = angularDisplacement.eulerAngles / Time.fixedDeltaTime;
			// angularDisplacement.ToAngleAxis(out var angle, out var angleAxis);
			// _imuAngularVelocity = angleAxis * angle / Time.fixedDeltaTime;

			// Debug.Log($"{_imuAngularVelocity} {angularDisplacement.eulerAngles / Time.fixedDeltaTime}");	

			var currentLinearVelocity = (currentPosition - _previousImuPosition) / Time.fixedDeltaTime;
			_imuLinearAcceleration = (currentLinearVelocity - _previousLinearVelocity) / Time.fixedDeltaTime;
			_imuLinearAcceleration.y += (-Physics.gravity.y);

			ApplyNoises(Time.fixedDeltaTime);

			_previousImuRotation = _imuRotation;
			_previousImuPosition = currentPosition;
			_previousLinearVelocity = currentLinearVelocity;

			_imuOrientation = _imuRotation.eulerAngles;
			var calculatedPitch = CalculatePitchFromForwardBaseAxis();
			_imuOrientation.x = calculatedPitch;
		}

		protected override void GenerateMessage()
		{
			_imu.Orientation.Set(_imuRotation);
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
			return _imuOrientation;
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