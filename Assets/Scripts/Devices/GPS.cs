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

		private Vector3 worldPosition;
		private Vector3 sensorVelocity;

		private Vector3 previousSensorPosition;
		private Vector3 gpsCoordinates;
		private Vector3 gpsVelocity;
		private Noise noise = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			gpsLink = transform.parent;
			DeviceName = name;

			sphericalCoordinates = DeviceHelper.GetGlobalSphericalCoordinates();
		}

		protected override void OnStart()
		{
			previousSensorPosition = gpsLink.position;
		}

		void Update()
		{
			worldPosition = gpsLink.position; // Get postion in Cartesian frame

			var positionDiff = worldPosition - previousSensorPosition;
			previousSensorPosition = worldPosition;

			sensorVelocity = positionDiff / Time.deltaTime;
		}

		protected override void InitializeMessages()
		{
			gps = new messages.Gps();
			gps.Time = new messages.Time();
			gps.LinkName = DeviceName;
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(gps.Time);

			// Apply position noise before converting to global frame
			// TODO: Applying noise

			// Convert to global frames
			var convertedPosition = DeviceHelper.Convert.Position(worldPosition);
			gpsCoordinates = sphericalCoordinates.SphericalFromLocal(convertedPosition);

			gps.LatitudeDeg = gpsCoordinates.x;
			gps.LongitudeDeg = gpsCoordinates.y;
			gps.Altitude = gpsCoordinates.z;

			// Convert to global frame
			var convertedVelocity = DeviceHelper.Convert.Position(sensorVelocity);
			gpsVelocity = sphericalCoordinates.GlobalFromLocal(convertedVelocity);

			// Apply noise after converting to global frame
			// TODO: Applying noise

			gps.VelocityEast = gpsVelocity.x;
			gps.VelocityNorth = gpsVelocity.y;
			gps.VelocityUp = gpsVelocity.z;

			PushDeviceMessage<messages.Gps>(gps);
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