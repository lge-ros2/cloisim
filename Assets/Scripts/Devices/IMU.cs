/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
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

		private readonly SensorNoiseChannels _angularVelocityNoise = new SensorNoiseChannels(new[] { "x", "y", "z" });
		private readonly SensorNoiseChannels _linearAccelerationNoise = new SensorNoiseChannels(new[] { "x", "y", "z" });

		private readonly object _snapshotLock = new object();

		public void SetupNoises(in SDFormat.ImuSensor element)
		{
			if (element == null)
				return;

			if (element.AngularVelocityXNoise.Type != SDFormat.NoiseType.None)
			{
				_angularVelocityNoise["x"] = new Noise(element.AngularVelocityXNoise);
			}

			if (element.AngularVelocityYNoise.Type != SDFormat.NoiseType.None)
			{
				_angularVelocityNoise["y"] = new Noise(element.AngularVelocityYNoise);
			}

			if (element.AngularVelocityZNoise.Type != SDFormat.NoiseType.None)
			{
				_angularVelocityNoise["z"] = new Noise(element.AngularVelocityZNoise);
			}

			if (element.LinearAccelerationXNoise.Type != SDFormat.NoiseType.None)
			{
				_linearAccelerationNoise["x"] = new Noise(element.LinearAccelerationXNoise);
			}

			if (element.LinearAccelerationYNoise.Type != SDFormat.NoiseType.None)
			{
				_linearAccelerationNoise["y"] = new Noise(element.LinearAccelerationYNoise);
			}

			if (element.LinearAccelerationZNoise.Type != SDFormat.NoiseType.None)
			{
				_linearAccelerationNoise["z"] = new Noise(element.LinearAccelerationZNoise);
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
			_previousImuPosition = transform.position;
			_previousLinearVelocity = Vector3.zero;
			// Debug.Log("_imuInitialRotation=" + _imuInitialRotation);
		}

		protected override void OnReset()
		{
			// Debug.Log("IMU Reset");
			_previousImuRotation = Quaternion.identity;
			_previousImuPosition = transform.position;
			_previousLinearVelocity = Vector3.zero;

			_imuOrientation = Vector3.zero;
			_imuAngularVelocity = Vector3.zero;
			_imuLinearAcceleration = Vector3.zero;
		}

		protected override void InitializeMessages()
		{
			_imu = new messages.Imu
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				},
				Orientation = new messages.Quaternion(),
				AngularVelocity = new messages.Vector3d(),
				LinearAcceleration = new messages.Vector3d()
			};
		}

		protected override void SetupMessages()
		{
			_imu.EntityName = DeviceName;
		}

		private void ApplyNoises(in float deltaTime)
		{
			_angularVelocityNoise.Apply("x", ref _imuAngularVelocity.x, deltaTime);
			_angularVelocityNoise.Apply("y", ref _imuAngularVelocity.y, deltaTime);
			_angularVelocityNoise.Apply("z", ref _imuAngularVelocity.z, deltaTime);

			_linearAccelerationNoise.Apply("x", ref _imuLinearAcceleration.x, deltaTime);
			_linearAccelerationNoise.Apply("y", ref _imuLinearAcceleration.y, deltaTime);
			_linearAccelerationNoise.Apply("z", ref _imuLinearAcceleration.z, deltaTime);
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
			lock (_snapshotLock)
			{
				var currentPosition = transform.position;

				// Calculate orientation and acceleration
				// Rotation from A to B : B * Quaternion.Inverse(A);
				_imuRotation = transform.rotation * Quaternion.Inverse(_imuInitialRotation);

				var angularDisplacement = _imuRotation * Quaternion.Inverse(_previousImuRotation);
				angularDisplacement.ToAngleAxis(out var angle, out var angleAxis);
				// Normalize angle to [-180, 180] to get shortest rotation path
				if (angle > 180f)
					angle -= 360f;
				_imuAngularVelocity = angleAxis * angle / Time.fixedDeltaTime;

				var currentLinearVelocity = (currentPosition - _previousImuPosition) / Time.fixedDeltaTime;
				_imuLinearAcceleration = (currentLinearVelocity - _previousLinearVelocity) / Time.fixedDeltaTime;
				_imuLinearAcceleration.y += -Physics.gravity.y;

				ApplyNoises(Time.fixedDeltaTime);

				_previousImuRotation = _imuRotation;
				_previousImuPosition = currentPosition;
				_previousLinearVelocity = currentLinearVelocity;

				_imuOrientation = _imuRotation.eulerAngles;
				var calculatedPitch = CalculatePitchFromForwardBaseAxis();
				_imuOrientation.x = calculatedPitch;
			}
		}

		protected override void GenerateMessage()
		{
			Quaternion rotation;
			Vector3 angularVelocity, linearAcceleration;
			lock (_snapshotLock)
			{
				rotation = _imuRotation;
				angularVelocity = _imuAngularVelocity;
				linearAcceleration = _imuLinearAcceleration;
			}

			_imu.Orientation.Set(rotation);
			_imu.AngularVelocity.Set(angularVelocity * Mathf.Deg2Rad);
			_imu.LinearAcceleration.Set(linearAcceleration);
			// Use fixed-dt synthetic time instead of physics-step SimTime
			// so consecutive IMU messages always have exactly UpdatePeriod apart.
			_imu.Header.Stamp.Set(GetNextSyntheticTime());

			PushDeviceMessage(_imu);
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