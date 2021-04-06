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

		public Vector3 spherical;
		public Vector3 gpsVelocity;

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

		void Update()
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
			spherical = sphericalCoordinates.SphericalFromLocal(worldPosition);

			gps.LatitudeDeg = spherical.z;
			gps.LongitudeDeg = -spherical.x;
			gps.Altitude = spherical.y;

			// Convert to global frame
			gpsVelocity = sphericalCoordinates.GlobalFromLocal(sensorVelocity);

			// Apply noise after converting to global frame
			// TODO: Applying noise

			gps.VelocityEast = gpsVelocity.z;
			gps.VelocityNorth = -gpsVelocity.x;
			gps.VelocityUp = gpsVelocity.y;

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