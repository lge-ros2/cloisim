/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

public class MicomSensor : Device
{
	private messages.Micom micomSensorData = null;

	protected override void OnAwake()
	{
	}

	protected override void OnStart()
	{
		deviceName = "MicomSensor";
	}

	protected override IEnumerator MainDeviceWorker()
	{
		while (true)
		{
			GenerateMessage();
			yield return new WaitForSeconds(WaitPeriod());
		}
	}

	protected override IEnumerator OnVisualize()
	{
		yield return null;
	}

	protected override void InitializeMessages()
	{
		micomSensorData = new messages.Micom();
		micomSensorData.Time = new messages.Time();
		micomSensorData.Imu = new messages.Imu();
		micomSensorData.Imu.EntityName = "IMU";
		micomSensorData.Imu.Stamp = new messages.Time();
		micomSensorData.Imu.Orientation = new messages.Quaternion();
		micomSensorData.Imu.AngularVelocity = new messages.Vector3d();
		micomSensorData.Imu.LinearAcceleration = new messages.Vector3d();
		micomSensorData.Accgyro = new messages.Micom.AccGyro();
		micomSensorData.Odom = new messages.Micom.Odometry();
	}

	protected override void GenerateMessage()
	{
		// Temporary
		DeviceHelper.SetCurrentTime(micomSensorData.Time);
		PushData<messages.Micom>(micomSensorData);
	}

	public bool SetIMU(in SensorDevices.IMU imuSensor)
	{
		var imu = micomSensorData.Imu;

		if (micomSensorData == null || imu == null)
		{
			return false;
		}

		if (imu.Orientation == null || imu.AngularVelocity == null || imu.LinearAcceleration == null)
		{
			return false;
		}

		var orientation = imuSensor.GetOrientation();
		imu.Orientation.X = orientation.x;
		imu.Orientation.Y = orientation.y;
		imu.Orientation.Z = orientation.z;
		imu.Orientation.W = orientation.w;

		var angularVelocity = imuSensor.GetAngularVelocity();
		imu.AngularVelocity.X = angularVelocity.x;
		imu.AngularVelocity.Y = angularVelocity.y;
		imu.AngularVelocity.Z = angularVelocity.z;

		var linearAcceleration = imuSensor.GetLinearAcceleration();
		imu.LinearAcceleration.X = linearAcceleration.x;
		imu.LinearAcceleration.Y = linearAcceleration.y;
		imu.LinearAcceleration.Z = linearAcceleration.z;

		DeviceHelper.SetCurrentTime(micomSensorData.Imu.Stamp);

		return true;
	}

	public bool SetAccGyro(in Vector3 angle)
	{
		var accGyro = micomSensorData.Accgyro;

		if (micomSensorData == null || accGyro == null)
		{
			return false;
		}

		accGyro.AngleX = angle.x;
		accGyro.AngleY = angle.y;
		accGyro.AngleZ = angle.z;

		accGyro.AccX = 0;
		accGyro.AccY = 0;
		accGyro.AccZ = 0;

		accGyro.AngulerRateX = 0;
		accGyro.AngulerRateY = 0;
		accGyro.AngulerRateZ = 0;

		return true;
	}

	public bool SetOdomData(in float linearVelocityLeft, in float linearVelocityRight)
	{
		const float M2MM = 1000.0f;

		if (micomSensorData != null)
		{
			var odom = micomSensorData.Odom;

			if (odom != null)
			{
				odom.SpeedLeft = (int)(linearVelocityLeft * Mathf.Deg2Rad * M2MM);
				odom.SpeedRight = (int)(linearVelocityRight * Mathf.Deg2Rad * M2MM);
				// Debug.LogFormat("Odom {0}, {1} ", linearVelocityLeft, linearVelocityRight);
				// Debug.LogFormat("Odom {0}, {1} ", odom.SpeedLeft, odom.SpeedRight);
				return true;
			}
		}

		return false;
	}
}
