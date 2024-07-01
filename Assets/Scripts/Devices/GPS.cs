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
		private messages.Gps _gps = null;

		private Transform _gpsLink = null;
		private SphericalCoordinates _sphericalCoordinates = null;

		private Vector3 _sensorInitialRotation = Vector3.zero;
		private Vector3 _sensorCurrentRotation = Vector3.zero;
		private Vector3 _worldFrameOrientation = Vector3.zero;

		public Vector3 _worldPosition;
		public Vector3 _sensorVelocity;

		private Vector3 _previousSensorPosition;
		public Vector3d _gpsCoordinates;
		public Vector3d _gpsVelocity;

		public Dictionary<string, Noise> position_sensing_noises = new Dictionary<string, Noise>()
		{
			{"horizontal", null},
			{"vertical", null}
		};

		public Dictionary<string, Noise> velocity_sensing_noises = new Dictionary<string, Noise>()
		{
			{"horizontal", null},
			{"vertical", null}
		};

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


		private void ApplyNoises()
		{
			if (position_sensing_noises["horizontal"] != null)
			{
				position_sensing_noises["horizontal"].Apply<double>(ref _gpsCoordinates.x);
			}

			if (position_sensing_noises["vertical"] != null)
			{
				position_sensing_noises["vertical"].Apply<double>(ref _gpsCoordinates.y);
			}

			if (velocity_sensing_noises["horizontal"] != null)
			{
				velocity_sensing_noises["horizontal"].Apply<double>(ref _gpsVelocity.x);
			}

			if (velocity_sensing_noises["vertical"] != null)
			{
				velocity_sensing_noises["vertical"].Apply<double>(ref _gpsVelocity.y);
			}
		}

		private void AssembleGPSMessage()
		{
			DeviceHelper.SetCurrentTime(_gps.Time);

			// Convert to global frames
			var convertedPosition = Unity2SDF.Position(_worldPosition);

			_gpsCoordinates = _sphericalCoordinates.SphericalFromLocal(convertedPosition);

			_gps.LatitudeDeg = _gpsCoordinates.x;
			_gps.LongitudeDeg = _gpsCoordinates.y;
			_gps.Altitude = _gpsCoordinates.z;

			// Convert to global frame
			var convertedVelocity = Unity2SDF.Position(_sensorVelocity);
			_gpsVelocity = _sphericalCoordinates.GlobalFromLocal(convertedVelocity);

			// Apply noise after converting to global frame
			ApplyNoises();

			_gps.VelocityNorth = _gpsVelocity.x;
			_gps.VelocityEast = _gpsVelocity.y;
			_gps.VelocityUp = _gpsVelocity.z;
		}

		public void AssembleHeadingMessage()
		{
			DeviceHelper.SetCurrentTime(_gps.Heading.Stamp);
			var sensorRotation = _sensorCurrentRotation - _sensorInitialRotation + _worldFrameOrientation;
			var sensorOrientation = Quaternion.Euler(sensorRotation.x, sensorRotation.y, sensorRotation.z);

			DeviceHelper.SetQuaternion(_gps.Heading.Orientation, sensorOrientation);
			DeviceHelper.SetVector3d(_gps.Heading.AngularVelocity, Vector3.zero);
			DeviceHelper.SetVector3d(_gps.Heading.LinearAcceleration, Vector3.zero);
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