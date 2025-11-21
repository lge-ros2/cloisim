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
		private class NoiseIMU
		{
			public Dictionary<string, Noise> angular_velocity;
			public Dictionary<string, Noise> linear_acceleration;
			public NoiseIMU(in Noise defaultNoise = null)
			{
				angular_velocity = new Dictionary<string, Noise>
				{
					{"x", defaultNoise},
					{"y", defaultNoise},
					{"z", defaultNoise}
				};
				
				linear_acceleration = new Dictionary<string, Noise>
				{
					{"x", defaultNoise},
					{"y", defaultNoise},
					{"z", defaultNoise}
				};
			}
		}

		private messages.Imu _imu = null;

		private Quaternion _imuInitialRotation = Quaternion.identity;
		private Quaternion _imuRotation = Quaternion.identity;

		private Vector3 _imuOrientation = Vector3.zero;
		private Vector3 _imuAngularVelocity = Vector3.zero;
		private Vector3 _imuLinearAcceleration = Vector3.zero;

		private Vector3 _previousImuPosition = Vector3.zero;
		private Quaternion _previousImuRotation = Quaternion.identity;
		private Vector3 _previousLinearVelocity = Vector3.zero;

		private NoiseIMU _noises = new NoiseIMU();

		public void SetupNoises(in SDF.IMU element)
		{
			if (element == null)
				return;

			Debug.Log($"{DeviceName}: Apply noise type:{element.type}");

			if (element.noise_angular_velocity.x != null)
			{
				_noises.angular_velocity["x"] = new Noise(element.noise_angular_velocity.x);
			}

			if (element.noise_angular_velocity.y != null)
			{
				_noises.angular_velocity["y"] = new Noise(element.noise_angular_velocity.y);
			}

			if (element.noise_angular_velocity.z != null)
			{
				_noises.angular_velocity["z"] = new Noise(element.noise_angular_velocity.z);
			}

			if (element.noise_linear_acceleration.x != null)
			{
				_noises.linear_acceleration["x"] = new Noise(element.noise_linear_acceleration.x);
			}

			if (element.noise_linear_acceleration.y != null)
			{
				_noises.linear_acceleration["y"] = new Noise(element.noise_linear_acceleration.y);
			}

			if (element.noise_linear_acceleration.z != null)
			{
				_noises.linear_acceleration["z"] = new Noise(element.noise_linear_acceleration.z);
			}
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
			Reset();
		}

		protected override void OnStart()
		{
			_imuInitialRotation = transform.rotation;
			// Debug.Log("_imuInitialRotation=" + _imuInitialRotation);
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			_previousImuRotation = Quaternion.identity;

			_imuOrientation = Vector3.zero;
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
			if (_noises.angular_velocity["x"] != null)
			{
				_noises.angular_velocity["x"].Apply<float>(ref _imuAngularVelocity.x, deltaTime);
			}

			if (_noises.angular_velocity["y"] != null)
			{
				_noises.angular_velocity["y"].Apply<float>(ref _imuAngularVelocity.y, deltaTime);
			}

			if (_noises.angular_velocity["z"] != null)
			{
				_noises.angular_velocity["z"].Apply<float>(ref _imuAngularVelocity.z, deltaTime);
			}

			if (_noises.linear_acceleration["x"] != null)
			{
				_noises.linear_acceleration["x"].Apply<float>(ref _imuLinearAcceleration.x, deltaTime);
			}

			if (_noises.linear_acceleration["y"] != null)
			{
				_noises.linear_acceleration["y"].Apply<float>(ref _imuLinearAcceleration.y, deltaTime);
			}

			if (_noises.linear_acceleration["z"] != null)
			{
				_noises.linear_acceleration["z"].Apply<float>(ref _imuLinearAcceleration.z, deltaTime);
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
			var pitch = -Mathf.Atan2(rotatedBase.y, horizontalDistance) * Mathf.Rad2Deg;

			// Debug.Log($"{rotatedBase.ToString("F5")}, {horizontalDistance.ToString("F5")}, {(rotatedBase * Mathf.Rad2Deg).ToString("F5")} , pitch={pitch}");

			// For roll, assuming this is relative to the base axis direction
			// var projectedZ = Vector3.ProjectOnPlane(rotatedBase, Vector3.forward);
			// var roll = Mathf.Atan2(projectedZ.y, projectedZ.x) * Mathf.Rad2Deg;

			// return new Vector3(newPitch, yaw, roll);
			return pitch;
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