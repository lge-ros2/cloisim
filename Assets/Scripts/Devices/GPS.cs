/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class GPS : Device
	{
		private messages.NavSatWithCovariance _navSat = null;

		private Transform _gpsLink = null;
		private SphericalCoordinates _sphericalCoordinates = null;

		private Vector3 _sensorInitialRotation = Vector3.zero;
		private Vector3 _sensorCurrentRotation = Vector3.zero;
		private Vector3 _worldFrameOrientation = Vector3.zero;

		private Vector3 _worldPosition;
		private Vector3 _sensorVelocity;

		private Vector3 _previousSensorPosition;

		private readonly SensorNoiseChannels _positionNoise = new SensorNoiseChannels(new[] { "horizontal", "vertical" });
		private readonly SensorNoiseChannels _velocityNoise = new SensorNoiseChannels(new[] { "horizontal", "vertical" });

		private readonly object _snapshotLock = new object();
		public void SetupNoises(in SDFormat.NavSatSensor element)
		{
			if (element == null)
				return;

			if (element.HorizontalPositionNoise.Type != SDFormat.NoiseType.None)
			{
				_positionNoise["horizontal"] = new Noise(element.HorizontalPositionNoise);
			}

			if (element.VerticalPositionNoise.Type != SDFormat.NoiseType.None)
			{
				_positionNoise["vertical"] = new Noise(element.VerticalPositionNoise);
			}

			if (element.HorizontalVelocityNoise.Type != SDFormat.NoiseType.None)
			{
				_velocityNoise["horizontal"] = new Noise(element.HorizontalVelocityNoise);
			}

			if (element.VerticalVelocityNoise.Type != SDFormat.NoiseType.None)
			{
				_velocityNoise["vertical"] = new Noise(element.VerticalVelocityNoise);
			}
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_gpsLink = transform.parent;
			DeviceName = name;

			_sphericalCoordinates = DeviceHelper.GetGlobalSphericalCoordinates();
			_worldFrameOrientation = Vector3.up * _sphericalCoordinates.HeadingAngle;
			// Debug.Log("worldFrameOrientation=" + _worldFrameOrientation.ToString("F3"));
		}

		protected override void OnStart()
		{
			_previousSensorPosition = _gpsLink.position;
			_sensorInitialRotation = transform.rotation.eulerAngles;
		}

		protected override void OnReset()
		{
			_previousSensorPosition = _gpsLink.position;
			_sensorVelocity = Vector3.zero;
		}

		protected override void InitializeMessages()
		{
			_navSat = new messages.NavSatWithCovariance
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				}
			};
		}

		protected override void SetupMessages()
		{
			_navSat.FrameId = DeviceName;
		}

		void Update()
		{
			lock (_snapshotLock)
			{
				_worldPosition = _gpsLink.position; // Get postion in Cartesian frame

				var positionDiff = _worldPosition - _previousSensorPosition;
				_previousSensorPosition = _worldPosition;

				_sensorVelocity = positionDiff / Time.deltaTime;

				_sensorCurrentRotation = transform.rotation.eulerAngles;
			}
		}

		private void ApplyNoises(ref Vector3d coordinates, ref Vector3d velocity)
		{
			_positionNoise.Apply("horizontal", ref coordinates.x);
			_positionNoise.Apply("vertical", ref coordinates.y);

			_velocityNoise.Apply("horizontal", ref velocity.x);
			_velocityNoise.Apply("vertical", ref velocity.y);
		}

		private void AssembleGPSMessage()
		{
			_navSat.Header.Stamp.SetCurrentTime();

			Vector3 position, velocity;
			lock (_snapshotLock)
			{
				position = _worldPosition;
				velocity = _sensorVelocity;
			}

			// Convert to global frames
			var convertedPosition = Unity2SDF.Position(position);
			convertedPosition.X *= -1;
			convertedPosition.Y *= -1;
			var gpsCoordinates = _sphericalCoordinates.SphericalFromLocal(convertedPosition);

			// Convert to global frame
			var velocityRHS = Unity2SDF.Position(velocity);
			var gpsVelocity = _sphericalCoordinates.GlobalFromLocal(velocityRHS);

			// Apply noise after converting to global frame
			ApplyNoises(ref gpsCoordinates, ref gpsVelocity);

			_navSat.LatitudeDeg = gpsCoordinates.x;
			_navSat.LongitudeDeg = gpsCoordinates.y;
			_navSat.Altitude = gpsCoordinates.z;

			_navSat.VelocityEast = gpsVelocity.x;
			_navSat.VelocityNorth = -gpsVelocity.y;
			_navSat.VelocityUp = gpsVelocity.z;
		}

		public void AssembleHeadingMessage()
		{
			// Heading (IMU) data was removed from Gps message in proto3
			// Heading is now handled separately if needed
		}

		protected override void GenerateMessage()
		{
			AssembleGPSMessage();
			AssembleHeadingMessage();

			PushDeviceMessage(_navSat);
		}

		public double Longitude => _navSat.LongitudeDeg;

		public double Latitude => _navSat.LatitudeDeg;

		public double Altitude => _navSat.Altitude;

		public double VelocityEast => _navSat.VelocityEast;

		public double VelocityNorth => _navSat.VelocityNorth;

		public double VelocityUp => _navSat.VelocityUp;

		public Vector3 VelocityENU()
		{
			return new Vector3((float)_navSat.VelocityEast, (float)_navSat.VelocityNorth, (float)_navSat.VelocityUp);
		}
	}
}