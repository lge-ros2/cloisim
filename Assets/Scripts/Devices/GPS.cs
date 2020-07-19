/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class GPS : Device
	{
		private gazebo.msgs.Gps gps = null;

		private SphericalCoordinates sphericalCoordinates = null;

		private Transform gpsLink = null;

		private Vector3 sensorVelocity;

		public Vector3 previousSensorPosition;

		public Vector3 spherical;
		public Vector3 gpsVelocity;

		protected override void OnAwake()
		{
			gpsLink = transform.parent;
			deviceName = name;

			var coreObject = GameObject.Find("Core");
			if (coreObject == null)
			{
				Debug.LogError("Failed to Find 'Core'!!!!");
			}
			else
			{
				sphericalCoordinates = coreObject.GetComponent<SphericalCoordinates>();
			}
		}

		protected override void OnStart()
		{
			previousSensorPosition = gpsLink.position;
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
		}

		void Update()
		{
			var positionDiff = gpsLink.position - previousSensorPosition;
			previousSensorPosition = gpsLink.position;

			sensorVelocity = positionDiff / Time.deltaTime;
		}

		protected override void InitializeMessages()
		{
			gps = new gazebo.msgs.Gps();
			gps.Time = new gazebo.msgs.Time();
			gps.LinkName = deviceName;
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();

			while (true)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(gps.Time);

			// Get postion in Cartesian gazebo frame
			var worldPosition = gpsLink.position;

			// Apply position noise before converting to global frame
			// TODO: Applying noise

			// Convert to global frames
			spherical = sphericalCoordinates.SphericalFromLocal(worldPosition);

			gps.LatitudeDeg = spherical.x;
			gps.LongitudeDeg = spherical.y;
			gps.Altitude = spherical.z;

			// Convert to global frame
			gpsVelocity = sphericalCoordinates.GlobalFromLocal(sensorVelocity);

			// Apply noise after converting to global frame
			// TODO: Applying noise

			gps.VelocityEast = gpsVelocity.x;
			gps.VelocityNorth = gpsVelocity.y;
			gps.VelocityUp = gpsVelocity.z;

			PushData<gazebo.msgs.Gps>(gps);
		}

		float Longitude()
		{
			return (float)gps.LongitudeDeg;
		}

		float Latitude()
		{
			return (float)gps.LatitudeDeg;
		}

		double Altitude()
		{
			return (float)gps.Altitude;
		}

		Vector3 VelocityENU()
		{
			return new Vector3((float)gps.VelocityEast, (float)gps.VelocityNorth, (float)gps.VelocityUp);
		}

		double VelocityEast()
		{
			return (float)gps.VelocityEast;
		}

		double VelocityNorth()
		{
			return (float)gps.VelocityNorth;
		}

		float VelocityUp()
		{
			return (float)gps.VelocityUp;
		}
	}
}