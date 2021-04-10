/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class GPS : Device
	{
		private messages.Gps gps = null;

		private SphericalCoordinates sphericalCoordinates = null;

		private Transform gpsLink = null;

		private Vector3 sensorVelocity;

		public Vector3 previousSensorPosition;

		public Vector3 _gpsCoordinates;
		public Vector3 _gpsVelocity;

		protected override void OnAwake()
		{
			_mode = Mode.TX;
			gpsLink = transform.parent;
			deviceName = name;

			sphericalCoordinates = DeviceHelper.GetSphericalCoordinates();
		}

		protected override void OnStart()
		{
			previousSensorPosition = gpsLink.position;
		}

		protected override void ProcessDeviceCoroutine()
		{
			var positionDiff = gpsLink.position - previousSensorPosition;
			previousSensorPosition = gpsLink.position;

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

			Debug.Log(_gpsCoordinates.ToString("F8"));
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