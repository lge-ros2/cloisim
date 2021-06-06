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
	public class GPS : Device
	{
		private messages.Gps gps = null;

		private SphericalCoordinates sphericalCoordinates = null;

		private Transform gpsLink = null;

		public Vector3 worldPosition;
		public Vector3 sensorVelocity;

		private Vector3 previousSensorPosition;
		public Vector3d gpsCoordinates;
		public Vector3d gpsVelocity;

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

			// Convert to global frames
			var convertedPosition = DeviceHelper.Convert.Position(worldPosition);

			gpsCoordinates = sphericalCoordinates.SphericalFromLocal(convertedPosition);

			// Apply noise after converting to global frame
			if (position_sensing_noises["horizontal"] != null)
			{
				position_sensing_noises["horizontal"].Apply<double>(ref gpsCoordinates.x);
			}

			if (position_sensing_noises["vertical"] != null)
			{
				position_sensing_noises["vertical"].Apply<double>(ref gpsCoordinates.y);
			}

			gps.LatitudeDeg = gpsCoordinates.x;
			gps.LongitudeDeg = gpsCoordinates.y;
			gps.Altitude = gpsCoordinates.z;

			// Convert to global frame
			var convertedVelocity = DeviceHelper.Convert.Position(sensorVelocity);
			gpsVelocity = sphericalCoordinates.GlobalFromLocal(convertedVelocity);

			// Apply noise after converting to global frame
			if (velocity_sensing_noises["horizontal"] != null)
			{
				velocity_sensing_noises["horizontal"].Apply<double>(ref gpsVelocity.x);
			}

			if (velocity_sensing_noises["vertical"] != null)
			{
				velocity_sensing_noises["vertical"].Apply<double>(ref gpsVelocity.y);
			}

			gps.VelocityNorth = gpsVelocity.x;
			gps.VelocityEast = gpsVelocity.y;
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