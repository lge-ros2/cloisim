/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class GPS : Device
	{
		private class NoiseGPS
		{
			public Dictionary<string, Noise> position_sensing;
			public Dictionary<string, Noise> velocity_sensing;

			public NoiseGPS(in Noise defaultNoise = null)
			{
				position_sensing = new Dictionary<string, Noise>
				{
					{"horizontal", defaultNoise},
					{"vertical", defaultNoise}
				};
				
				velocity_sensing = new Dictionary<string, Noise>
				{
					{"horizontal", defaultNoise},
					{"vertical", defaultNoise}
				};
			}
		}
		private messages.Gps _gps = null;

		private Transform _gpsLink = null;
		private SphericalCoordinates _sphericalCoordinates = null;

		private Vector3 _sensorInitialRotation = Vector3.zero;
		private Vector3 _sensorCurrentRotation = Vector3.zero;
		private Vector3 _worldFrameOrientation = Vector3.zero;

		public Vector3 _worldPosition;
		public Vector3 _sensorVelocity;

		private Vector3 _previousSensorPosition;
		// public Vector3d _gpsCoordinates;
		// public Vector3d _gpsVelocity;

		private NoiseGPS _noises = new NoiseGPS();

		public void SetupNoises(in SDF.NavSat element)
		{
			if (element == null)
				return;

			Debug.Log($"{DeviceName}: Apply noise type:{element.type}");

			if (element.position_sensing.horizontal_noise != null)
			{
				_noises.position_sensing["horizontal"] = new SensorDevices.Noise(element.position_sensing.horizontal_noise);
			}

			if (element.position_sensing.vertical_noise != null)
			{
				_noises.position_sensing["vertical"] = new SensorDevices.Noise(element.position_sensing.vertical_noise);
			}

			if (element.velocity_sensing.horizontal_noise != null)
			{
				_noises.velocity_sensing["horizontal"] = new SensorDevices.Noise(element.velocity_sensing.horizontal_noise);
			}

			if (element.velocity_sensing.vertical_noise != null)
			{
				_noises.velocity_sensing["vertical"] = new SensorDevices.Noise(element.velocity_sensing.vertical_noise);
			}
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			_gpsLink = transform.parent;
			DeviceName = name;

			_sphericalCoordinates = DeviceHelper.GetGlobalSphericalCoordinates();
			_worldFrameOrientation = (Vector3.up * _sphericalCoordinates.HeadingAngle);
			// Debug.Log("worldFrameOrientation=" + _worldFrameOrientation.ToString("F3"));
		}

		protected override void OnStart()
		{
			_previousSensorPosition = _gpsLink.position;
		}

		protected override void InitializeMessages()
		{
			_gps = new messages.Gps();
			_gps.Time = new messages.Time();
			_gps.Heading = new messages.Imu();
			_gps.Heading.Stamp = new messages.Time();
			_gps.Heading.Orientation = new messages.Quaternion();
			_gps.Heading.AngularVelocity = new messages.Vector3d();
			_gps.Heading.LinearAcceleration = new messages.Vector3d();
		}

		protected override void SetupMessages()
		{
			_gps.LinkName = DeviceName;
			_gps.Heading.EntityName = DeviceName + "_heading";
		}

		void Update()
		{
			_worldPosition = _gpsLink.position; // Get postion in Cartesian frame

			var positionDiff = _worldPosition - _previousSensorPosition;
			_previousSensorPosition = _worldPosition;

			_sensorVelocity = positionDiff / Time.deltaTime;

			_sensorCurrentRotation = transform.rotation.eulerAngles;
		}

		private void ApplyNoises(ref Vector3d coordinates, ref Vector3d velocity)
		{
			if (_noises.position_sensing["horizontal"] != null)
			{
				_noises.position_sensing["horizontal"].Apply<double>(ref coordinates.x);
			}

			if (_noises.position_sensing["vertical"] != null)
			{
				_noises.position_sensing["vertical"].Apply<double>(ref coordinates.y);
			}

			if (_noises.velocity_sensing["horizontal"] != null)
			{
				_noises.velocity_sensing["horizontal"].Apply<double>(ref velocity.x);
			}

			if (_noises.velocity_sensing["vertical"] != null)
			{
				_noises.velocity_sensing["vertical"].Apply<double>(ref velocity.y);
			}
		}

		private void AssembleGPSMessage()
		{
			_gps.Time.SetCurrentTime();

			// Convert to global frames
			var convertedPosition = Unity2SDF.Position(_worldPosition);
			convertedPosition.X *= -1;
			convertedPosition.Y *= -1;
			var gpsCoordinates = _sphericalCoordinates.SphericalFromLocal(convertedPosition);

			_gps.LatitudeDeg = gpsCoordinates.x;
			_gps.LongitudeDeg = gpsCoordinates.y;
			_gps.Altitude = gpsCoordinates.z;

			// Convert to global frame
			var velocityRHS = Unity2SDF.Position(_sensorVelocity);
			var gpsVelocity = _sphericalCoordinates.GlobalFromLocal(velocityRHS);

			_gps.VelocityEast = gpsVelocity.x;
			_gps.VelocityNorth = -gpsVelocity.y;
			_gps.VelocityUp = gpsVelocity.z;
			// Debug.Log($"{_gps.VelocityEast} {_gps.VelocityNorth} {_gps.VelocityUp}");

			// Apply noise after converting to global frame
			ApplyNoises(ref gpsCoordinates, ref gpsVelocity);
		}

		public void AssembleHeadingMessage()
		{
			_gps.Heading.Stamp.SetCurrentTime();
			var sensorRotation = _sensorCurrentRotation - _sensorInitialRotation + _worldFrameOrientation;
			var sensorOrientation = Quaternion.Euler(sensorRotation.x, sensorRotation.y, sensorRotation.z);

			_gps.Heading.Orientation.Set(sensorOrientation);
			_gps.Heading.AngularVelocity.Set(Vector3.zero);
			_gps.Heading.LinearAcceleration.Set(Vector3.zero);
		}

		protected override void GenerateMessage()
		{
			AssembleGPSMessage();
			AssembleHeadingMessage();

			PushDeviceMessage<messages.Gps>(_gps);
		}

		public double Longitude => _gps.LongitudeDeg;

		public double Latitude => _gps.LatitudeDeg;

		public double Altitude => _gps.Altitude;

		public double VelocityEast => _gps.VelocityEast;

		public double VelocityNorth => _gps.VelocityNorth;

		public double VelocityUp => _gps.VelocityUp;

		public Vector3 VelocityENU()
		{
			return new Vector3((float)_gps.VelocityEast, (float)_gps.VelocityNorth, (float)_gps.VelocityUp);
		}
	}
}