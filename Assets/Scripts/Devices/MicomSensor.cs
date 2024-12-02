/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class MicomSensor : Device
	{
		private messages.Micom micomSensorData = null;
		private MotorControl _motorControl = null;
		private SensorDevices.Battery battery = null;
		private SensorDevices.IMU _imuSensor = null;
		private List<SensorDevices.Sonar> _ussSensors = new List<SensorDevices.Sonar>();
		private List<SensorDevices.Sonar> _irSensors = new List<SensorDevices.Sonar>();
		// private List<SensorDevices.Magnet> magnetSensors = null;
		private List<SensorDevices.Contact> _bumperSensors = new List<SensorDevices.Contact>();


		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "MicomSensor";
		}

		protected override void OnStart()
		{
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
		}

		public void SetMotorControl(in MotorControl motorControl)
		{
			this._motorControl = motorControl;
		}

		public void SetIMU(in string sensorName)
		{
			var imuList = gameObject.GetComponentsInChildren<SensorDevices.IMU>();
			foreach (var imu in imuList)
			{
				if (imu.DeviceName.Contains($"::{sensorName}::") || // Model name
					imu.DeviceName.EndsWith($"::{sensorName}") || // Link name
					imu.name.Equals(sensorName))
				{
					Debug.Log($"IMU: {imu.DeviceName} attached to Micom");
					_imuSensor = imu;
					break;
				}
			}
		}

		public void SetUSS(in List<string> ussList)
		{
			var sonarList = GetComponentsInChildren<SensorDevices.Sonar>();

			foreach (var ussName in ussList)
			{
				foreach (var sonar in sonarList)
				{
					if (sonar.DeviceName.Contains($"::{ussName}::") || // Model name
						sonar.DeviceName.EndsWith($"::{ussName}") || // Link name
						sonar.name.Equals(ussName))
					{
						Debug.Log($"USS: {sonar.DeviceName} attached to Micom");
						_ussSensors.Add(sonar);
						break;
					}
				}
			}

			micomSensorData.uss.Distances = new double[_ussSensors.Count];
		}

		public void SetIRSensor(in List<string> irList)
		{
			var sonarList = GetComponentsInChildren<SensorDevices.Sonar>();

			foreach (var irName in irList)
			{
				foreach (var sonar in sonarList)
				{
					if (sonar.DeviceName.Contains($"::{irName}::") || // Model name
						sonar.DeviceName.EndsWith($"::{irName}") || // Link name
						sonar.name.Equals(irName))
					{
						Debug.Log($"IR: {sonar.DeviceName} attached to Micom");
						_irSensors.Add(sonar);
						break;
					}
				}
			}

			micomSensorData.ir.Distances = new double[_irSensors.Count];
		}

		public void SetMagnet(in List<string> magnetList)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
			Debug.Log("Magnet is to be implemented");
		}

		public void SetBumper(in List<string> bumperList)
		{
			var contactList = GetComponentsInChildren<SensorDevices.Contact>();

			foreach (var bumperName in bumperList)
			{
				foreach (var contact in contactList)
				{
					if (contact.DeviceName.Contains($"::{bumperName}::") || // Model name
						contact.DeviceName.EndsWith($"::{bumperName}") || // Link name
						contact.name.Equals(bumperName))
					{
						Debug.Log($"Bumper: {contact.DeviceName} attached to Micom");
						_bumperSensors.Add(contact);
						break;
					}
				}
			}

			micomSensorData.bumper.Bumpeds = new bool[_bumperSensors.Count];
		}

		public void SetBattery(in SensorDevices.Battery targetBattery)
		{
			micomSensorData.Battery = new messages.Battery();

			this.battery = targetBattery;
			micomSensorData.Battery.Name = battery.Name;
		}

		protected override void OnReset()
		{
			if (_imuSensor != null)
			{
				_imuSensor.Reset();
			}
		}

		protected override void InitializeMessages()
		{
			micomSensorData = new messages.Micom();
			micomSensorData.Time = new messages.Time();
			micomSensorData.Odom = new messages.Micom.Odometry();
			micomSensorData.Odom.AngularVelocity = new messages.Micom.Odometry.Wheel();
			micomSensorData.Odom.LinearVelocity = new messages.Micom.Odometry.Wheel();
			micomSensorData.Odom.Pose = new messages.Vector3d();
			micomSensorData.Odom.TwistLinear = new messages.Vector3d();
			micomSensorData.Odom.TwistAngular = new messages.Vector3d();
			micomSensorData.uss = new messages.Micom.Uss();
			micomSensorData.ir = new messages.Micom.Ir();
			micomSensorData.bumper = new messages.Micom.Bumper();
		}

		protected override void GenerateMessage()
		{
			PushDeviceMessage<messages.Micom>(micomSensorData);
		}

		void FixedUpdate()
		{
			if (micomSensorData == null)
			{
				Debug.LogWarning("micomSensorData is NULL");
				return;
			}

			var deltaTime = Time.fixedDeltaTime;

			if (battery != null)
			{
				micomSensorData.Battery.Voltage = battery.Update(deltaTime);
			}

			if (_motorControl != null)
			{
				if (_motorControl.Update(micomSensorData.Odom, deltaTime, _imuSensor) == false)
				{
					Debug.LogWarning("Update failed in MotorControl");
				}
			}

			UpdateIMU();
			UpdateUss();
			UpdateIr();
			UpdateBumper();

			micomSensorData.Time.Set(DeviceHelper.GlobalClock.FixedSimTime);
		}

		private void UpdateBumper()
		{
			if (micomSensorData == null || micomSensorData.bumper == null)
			{
				return;
			}

			for (var index = 0; index < _bumperSensors.Count; index++)
			{
				var bumperSensor = _bumperSensors[index];
				micomSensorData.bumper.Bumpeds[index] = bumperSensor.IsContacted();
				// Debug.Log(micomSensorData.bumper.Bumpeds[index]);
			}
		}

		private void UpdateUss()
		{
			if (micomSensorData == null || micomSensorData.uss == null)
			{
				return;
			}

			for (var index = 0; index < _ussSensors.Count; index++)
			{
				micomSensorData.uss.Distances[index] = _ussSensors[index].GetDetectedRange();
			}
		}

		private void UpdateIr()
		{
			if (micomSensorData == null || micomSensorData.ir == null)
			{
				return;
			}

			for (var index = 0; index < _irSensors.Count; index++)
			{
				micomSensorData.ir.Distances[index] = _irSensors[index].GetDetectedRange();
			}
		}

		private void UpdateIMU()
		{
			if (_imuSensor == null || micomSensorData == null)
			{
				return;
			}

			micomSensorData.Imu = _imuSensor.GetImuMessage();
		}
	}
}