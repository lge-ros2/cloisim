/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class GPS : Device
	{
		private messages.Gps gps = null;

		private SphericalCoordinates sphericalCoordinates = null;

		private Transform gpsLink = null;

		private Vector3 sensorVelocity;

		private Vector3 _previousSensorPosition;

		private Vector3 _gpsCoordinates;
		private Vector3 _gpsVelocity;

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			gpsLink = transform.parent;
			deviceName = name;

			sphericalCoordinates = DeviceHelper.GetSphericalCoordinates();
		}

		protected override void OnStart()
		{
			_previousSensorPosition = gpsLink.position;
		}

		protected override void ProcessDeviceCoroutine()
		{
			var positionDiff = gpsLink.position - _previousSensorPosition;
			_previousSensorPosition = gpsLink.position;

			sensorVelocity = positionDiff / Time.deltaTime;
		}

		protected override void InitializeMessages()
		{
			gps = new messages.Gps();
			gps.Time = new messages.Time();
			gps.LinkName = deviceName;
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(gps.Time);

			// Get postion in Cartesian frame
			var worldPosition = gpsLink.position;

			// Apply position noise before converting to global frame
			// TODO: Applying noise

			// Convert to global frames
			var convertedPosition = DeviceHelper.Convert.Position(worldPosition);
			_gpsCoordinates = sphericalCoordinates.SphericalFromLocal(convertedPosition);

			gps.LatitudeDeg = _gpsCoordinates.x;
			gps.LongitudeDeg = _gpsCoordinates.y;
			gps.Altitude = _gpsCoordinates.z;

			// Convert to global frame
			var convertedVelocity = DeviceHelper.Convert.Position(sensorVelocity);
			_gpsVelocity = sphericalCoordinates.GlobalFromLocal(convertedVelocity);

			// Apply noise after converting to global frame
			// TODO: Applying noise

			gps.VelocityEast = _gpsVelocity.x;
			gps.VelocityNorth = _gpsVelocity.y;
			gps.VelocityUp = _gpsVelocity.z;

			PushData<messages.Gps>(gps);
		}

		public double Longitude => gps.LongitudeDeg;

		public double Latitude => gps.LatitudeDeg;

		public double Altitude => gps.Altitude;

		public double VelocityEast => gps.VelocityEast;

		public double VelocityNorth => gps.VelocityNorth;

		public double VelocityUp => gps.VelocityUp;

		public Vector3 VelocityENU()
		{
			return new Vector3((float)gps.VelocityEast, (float)gps.VelocityNorth, (float)gps.VelocityUp);
		}

	}
}