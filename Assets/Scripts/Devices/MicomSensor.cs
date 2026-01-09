/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System.Text;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class MicomSensor : Device
	{
		private messages.Battery _batteryData = null;
		private messages.Micom.Odometry _odomData = null;

		private MotorControl _motorControl = null;
		private SensorDevices.Battery _battery = null;
		private SensorDevices.IMU _imuSensor = null;
		private List<SensorDevices.Sonar> _ussSensors = new();
		private List<SensorDevices.Sonar> _irSensors = new();
		// private List<SensorDevices.Magnet> magnetSensors = null;
		private List<SensorDevices.Contact> _bumperSensors = new();

		private float _accumulatedTime = 0f;

		private StringBuilder _log = new StringBuilder();

		public void PrintSensors()
		{
			Debug.Log(_log.ToString());
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "MicomSensor";
			_log.Clear();
			_log.AppendLine($"Attached Sensor in ({DeviceName})");
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
					_log.AppendLine($"IMU: {imu.DeviceName} attached to Micom");
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
						_log.AppendLine($"USS: {sonar.DeviceName} attached to Micom");
						_ussSensors.Add(sonar);
						break;
					}
				}
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
						_log.AppendLine($"IR: {sonar.DeviceName} attached to Micom");
						_irSensors.Add(sonar);
						break;
					}
				}
			}
		}

		public void SetMagnet(in List<string> magnetList)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
			_log.AppendLine("Magnet is to be implemented");
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
						_log.AppendLine($"Bumper: {contact.DeviceName} attached to Micom");
						_bumperSensors.Add(contact);
						break;
					}
				}
			}
		}

		public void SetBattery(in SensorDevices.Battery targetBattery)
		{
			_batteryData = new messages.Battery();

			this._battery = targetBattery;
			_batteryData.Name = _battery.Name;
		}

		protected override void OnReset()
		{
			_imuSensor?.Reset();
		}

		protected override void InitializeMessages()
		{
			_odomData = new messages.Micom.Odometry();
			_odomData.AngularVelocity = new messages.Micom.Odometry.Wheel();
			_odomData.LinearVelocity = new messages.Micom.Odometry.Wheel();
			_odomData.Pose = new messages.Vector3d();
			_odomData.Twist = new messages.Twist();
			_odomData.Twist.Linear = new messages.Vector3d();
			_odomData.Twist.Angular = new messages.Vector3d();
		}

		void FixedUpdate()
		{
			var delta = Time.fixedDeltaTime;

			_accumulatedTime += delta;

			if (_motorControl?.Update(_odomData, Time.fixedDeltaTime, _imuSensor) == false)
			{
				Debug.LogWarning("Update failed in MotorControl");
			}

			if (_accumulatedTime < UpdatePeriod)
				return;

			 _accumulatedTime -= UpdatePeriod;

			var micomSensorData = new messages.Micom();
			micomSensorData.Time = new messages.Time();
			micomSensorData.Time.Set(DeviceHelper.GlobalClock.FixedSimTime);

			UpdateBattery(micomSensorData, delta);
			UpdateUss(micomSensorData);
			UpdateIr(micomSensorData);
			UpdateBumper(micomSensorData);
			UpdateIMU(micomSensorData);

			micomSensorData.Odom = _odomData;

			_messageQueue.Enqueue(micomSensorData);
		}

		private void UpdateBattery(messages.Micom micomData, in float deltaTime)
		{
			if (_battery != null)
			{
				_batteryData.Voltage = _battery.Update(deltaTime);
				micomData.Battery = _batteryData;
			}
		}

		private void UpdateBumper(messages.Micom micomData)
		{
			if (micomData != null)
			{
				for (var index = 0; index < _bumperSensors.Count; index++)
				{
					var bumperSensor = _bumperSensors[index];
					var bumper = new messages.Micom.Bumper();
					bumper.Bumped = bumperSensor.IsContacted();
					bumper.Contacts = bumperSensor.GetContacts();

					micomData.Bumpers.Add(bumper);
					// _log.AppendLine(micomData.Bumpers.Count + " " + bumper.Bumped + ", " + bumper.Contacts);
				}
			}
		}

		private void UpdateUss(messages.Micom micomData)
		{
			if (micomData != null)
			{
				for (var index = 0; index < _ussSensors.Count; index++)
				{
					var ussSensor = _ussSensors[index];
					var uss = new messages.Micom.Uss();
					uss.Distance = ussSensor.GetDetectedRange();
					uss.State = ussSensor.GetSonar().Sonar;

					micomData.Usses.Add(uss);
				}
			}
		}

		private void UpdateIr(messages.Micom micomData)
		{
			if (micomData != null)
			{
				for (var index = 0; index < _irSensors.Count; index++)
				{
					var irSensor =_irSensors[index];
					var ir = new messages.Micom.Ir();
					ir.Distance = irSensor.GetDetectedRange();
					ir.State = irSensor.GetSonar().Sonar;

					micomData.Irs.Add(ir);
				}
			}
		}

		private void UpdateIMU(messages.Micom micomData)
		{
			if (_imuSensor != null && micomData != null)
			{
				micomData.Imu = _imuSensor.GetImuMessage();
			}
		}
	}
}