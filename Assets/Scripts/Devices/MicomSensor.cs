/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

public class MicomSensor : Device
{
	private PluginParameters parameters = null;
	private messages.Micom micomSensorData = null;

	private Motor motorLeft = null;
	private Motor motorRight = null;

	public SensorDevices.IMU imuSensor = null;
	public List<SensorDevices.Sonar> ussSensors = null;
	public List<SensorDevices.Sonar> irSensors = null;
	// private List<SensorDevices.Magnet> magnetSensors = null;
	// private List<SensorDevices.Switch> switchSensors = null;

	public float wheelRadius = 0.0f; // in mether
	private float divideWheelRadius = 0.0f; // for computational performacne.

	protected override void OnAwake()
	{
		imuSensor = gameObject.GetComponentInChildren<SensorDevices.IMU>();

		ussSensors = new List<SensorDevices.Sonar>();
		irSensors = new List<SensorDevices.Sonar>();

		deviceName = "MicomSensor";
	}

	protected override void OnStart()
	{
		const float MM2M = 0.001f;
		var modelList = GetComponentsInChildren<ModelPlugin>();

		var updateRate = parameters.GetValue<float>("update_rate", 20);
		SetUpdateRate(updateRate);

		var kp = parameters.GetValue<float>("PID/kp");
		var ki = parameters.GetValue<float>("PID/ki");
		var kd = parameters.GetValue<float>("PID/kd");

		var pidControl = new PID(kp, ki, kd);

		var wheelBase = parameters.GetValue<float>("wheel/base") * MM2M;
		wheelRadius = parameters.GetValue<float>("wheel/radius") * MM2M;
		divideWheelRadius = 1.0f/wheelRadius;

		var wheelNameLeft = parameters.GetValue<string>("wheel/location[@type='left']");
		var wheelNameRight = parameters.GetValue<string>("wheel/location[@type='right']");

		var motorFriction = parameters.GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = parameters.GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		foreach (var model in modelList)
		{
			// Debug.Log(model.name);
			if (model.name.Equals(wheelNameLeft))
			{
				var jointWheelLeft = model.GetComponentInChildren<HingeJoint>();
				motorLeft = new Motor("Left", jointWheelLeft, pidControl);

				var wheelLeftBody = jointWheelLeft.gameObject.GetComponent<Rigidbody>();

				// Debug.Log("joint Wheel Left found : " + jointWheelLeft.name);
				// Debug.Log("joint Wheel Left max angular velocity : " + jointWheelLeft.gameObject.GetComponent<Rigidbody>().maxAngularVelocity);
			}
			else if (model.name.Equals(wheelNameRight))
			{
				var jointWheelRight = model.GetComponentInChildren<HingeJoint>();
				motorRight = new Motor("Right", jointWheelRight, pidControl);

				var wheelRightBody = jointWheelRight.gameObject.GetComponent<Rigidbody>();

				// Debug.Log("joint Wheel Right found : " + jointWheelRight.name);
				// Debug.Log("joint Wheel Right max angular velocity : " + jointWheelRight.gameObject.GetComponent<Rigidbody>().maxAngularVelocity);
			}

			if (motorLeft != null && motorRight != null)
			{
				break;
			}
		}

		if (parameters.GetValues<string>("uss/sensor", out var ussList))
		{
			foreach (var model in modelList)
			{
				foreach (var uss in ussList)
				{
					if (model.name.Equals(uss))
					{
						var sonarSensor = model.GetComponentInChildren<SensorDevices.Sonar>();
						ussSensors.Add(sonarSensor);
						// Debug.Log("ussSensor found : " + sonarSensor.name);
					}
				}
				micomSensorData.uss.Distances = new uint[ussList.Count];
			}
		}

		if (parameters.GetValues<string>("ir/sensor", out var irList))
		{
			foreach (var model in modelList)
			{
				foreach (var ir in irList)
				{
					if (model.name.Equals(ir))
					{
						var sonarSensor = model.GetComponentInChildren<SensorDevices.Sonar>();
						irSensors.Add(sonarSensor);
						// Debug.Log("irSensor found : " + sonarSensor.name);
					}
				}
				micomSensorData.ir.Distances = new uint[irList.Count];
			}
		}

		if (parameters.GetValues<string>("magnet/sensor", out var magnetList))
		{
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
		}

		if (parameters.GetValues<string>("bumper/sensor", out var bumperList))
		{
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
		}
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
		micomSensorData.uss = new messages.Micom.Uss();
		micomSensorData.ir = new messages.Micom.Ir();
	}

	protected override void GenerateMessage()
	{
		// Temporary
		DeviceHelper.SetCurrentTime(micomSensorData.Time);
		PushData<messages.Micom>(micomSensorData);
	}

	void FixedUpdate()
	{
		UpdateIMU();
		UpdateAccGyro();
		UpdateUss();
		UpdateIr();
	}

	private void UpdateUss()
	{
		if ((micomSensorData == null || micomSensorData.uss == null))
		{
			return;
		}

		const float M2MM = 1000.0f;
		var index = 0;
		foreach (var uss in ussSensors)
		{
			micomSensorData.uss.Distances[index++] = (uint)(uss.GetDetectedRange() * M2MM);
		}
	}

	private void UpdateIr()
	{
		if ((micomSensorData == null || micomSensorData.ir == null))
		{
			return;
		}

		const float M2MM = 1000.0f;
		var index = 0;
		foreach (var ir in irSensors)
		{
			micomSensorData.ir.Distances[index++] = (uint)(ir.GetDetectedRange() * M2MM);
		}
	}

	private void UpdateIMU()
	{
		var imu = micomSensorData.Imu;

		if ((imuSensor == null) ||
			(micomSensorData == null || imu == null) ||
			(imu.Orientation == null || imu.AngularVelocity == null || imu.LinearAcceleration == null))
		{
			return;
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
	}

	private void UpdateAccGyro()
	{
		var localRotation = transform.rotation;
		var angle = localRotation.eulerAngles;

		var accGyro = micomSensorData.Accgyro;

		if (micomSensorData == null || accGyro == null)
		{
			return;
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
	}

	private bool SetOdomData(in float linearVelocityLeft, in float linearVelocityRight)
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

	public void SetMotorVelocity(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var angularVelocityLeft = linearVelocityLeft * divideWheelRadius;
		var angularVelocityRight = linearVelocityRight * divideWheelRadius;

		if (motorLeft != null && motorRight != null)
		{
			motorLeft.SetVelocityTarget(angularVelocityLeft);
			motorRight.SetVelocityTarget(angularVelocityRight);

			var linearJointVelocityLeft = motorLeft.GetCurrentVelocity() * wheelRadius;
			var linearJointVelocityRight = motorRight.GetCurrentVelocity() * wheelRadius;

			SetOdomData(linearJointVelocityLeft, linearJointVelocityRight);
		}
	}

	public void SetPluginParameter(in PluginParameters pluginParams)
	{
		parameters = pluginParams;
	}
}