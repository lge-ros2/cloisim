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
		private messages.Micom _micomSensorData = null;
		private MotorControl _motorControl = null;
		private SensorDevices.Battery _battery = null;
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

			_micomSensorData.Usses.Capacity =_ussSensors.Count;
			for (var i = 0; i < _micomSensorData.Usses.Capacity; i++)
			{
				var uss = new messages.Micom.Uss();
				_micomSensorData.Usses[i] = uss;
			}
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

			_micomSensorData.Irs.Capacity =_irSensors.Count;
			for (var i = 0; i < _micomSensorData.Irs.Capacity; i++)
			{
				var ir = new messages.Micom.Ir();
				_micomSensorData.Irs.Add(ir);
			}
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

			_micomSensorData.Bumpers.Capacity =_bumperSensors.Count;
			for (var i = 0; i < _micomSensorData.Bumpers.Capacity; i++)
			{
				var bumper = new messages.Micom.Bumper();
				bumper.Contacts = new messages.Contacts();
				_micomSensorData.Bumpers.Add(bumper);
			}
		}

		public void SetBattery(in SensorDevices.Battery targetBattery)
		{
			_micomSensorData.Battery = new messages.Battery();

			this._battery = targetBattery;
			_micomSensorData.Battery.Name = _battery.Name;
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
			_micomSensorData = new messages.Micom();
			_micomSensorData.Time = new messages.Time();
			_micomSensorData.Odom = null;
		}

		private void InitializeOdometryMessage()
		{
			_micomSensorData.Odom = new messages.Micom.Odometry();
			_micomSensorData.Odom.AngularVelocity = new messages.Micom.Odometry.Wheel();
			_micomSensorData.Odom.LinearVelocity = new messages.Micom.Odometry.Wheel();
			_micomSensorData.Odom.Pose = new messages.Vector3d();
			_micomSensorData.Odom.Twist = new messages.Twist();
			_micomSensorData.Odom.Twist.Linear = new messages.Vector3d();
			_micomSensorData.Odom.Twist.Angular = new messages.Vector3d();
		}

		protected override void GenerateMessage()
		{
			UpdateBattery(UpdatePeriod);
			UpdateIMU();
			UpdateUss();
			UpdateIr();
			UpdateBumper();

			PushDeviceMessage<messages.Micom>(_micomSensorData);
		}

		void FixedUpdate()
		{
			var delta = Time.fixedDeltaTime;

			if (_micomSensorData == null)
			{
				Debug.LogWarning("_micomSensorData is NULL");
				return;
			}

			if (_motorControl != null)
			{
				if (_micomSensorData.Odom == null)
				{
					InitializeOdometryMessage();
				}

				if (_motorControl.Update(_micomSensorData.Odom, delta, _imuSensor) == false)
				{
					Debug.LogWarning("Update failed in MotorControl");
				}
			}

			_micomSensorData.Time.Set(DeviceHelper.GlobalClock.FixedSimTime);
		}

		private void UpdateBattery(float deltaTime)
		{
			if (_battery != null && _micomSensorData != null)
			{
				_micomSensorData.Battery.Voltage = _battery.Update(deltaTime);
			}
		}

		private void UpdateBumper()
		{
			if (_micomSensorData != null)
			{
				for (var index = 0; index < _bumperSensors.Count; index++)
				{
					var bumper = _micomSensorData.Bumpers[index];
					var bumperSensor = _bumperSensors[index];
					bumper.Bumped = bumperSensor.IsContacted();
					bumper.Contacts = bumperSensor.GetContacts();

					_micomSensorData.Bumpers[index] = bumper;
					// Debug.Log(_micomSensorData.Bumpers.Count + " " + bumper.Bumped + ", " + bumper.Contacts);
				}
			}
		}

		private void UpdateUss()
		{
			if (_micomSensorData != null)
			{
				for (var index = 0; index < _ussSensors.Count; index++)
				{
					var uss = _micomSensorData.Usses[index];
					uss.Distance = _ussSensors[index].GetDetectedRange();
					uss.State = _ussSensors[index].GetSonar().Sonar;

					_micomSensorData.Usses[index] = uss;
				}
			}
		}

		private void UpdateIr()
		{
			if (_micomSensorData != null)
			{
				for (var index = 0; index < _irSensors.Count; index++)
				{
					var ir = _micomSensorData.Irs[index];
					ir.Distance = _irSensors[index].GetDetectedRange();
					ir.State = _irSensors[index].GetSonar().Sonar;

					_micomSensorData.Irs[index] = ir;
				}
			}
		}

		private void UpdateIMU()
		{
			if (_imuSensor != null && _micomSensorData != null)
			{
				_micomSensorData.Imu = _imuSensor.GetImuMessage();
			}
		}
	}
}