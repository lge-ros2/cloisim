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

		private readonly List<messages.Micom.Bumper> _bumperMessages = new();
		private readonly List<messages.Micom.Uss> _ussMessages = new();
		private readonly List<messages.Micom.Ir> _irMessages = new();


		private MotorControl _motorControl = null;
		private Battery _battery = null;
		private IMU _imuSensor = null;
		private List<Sonar> _ussSensors = new();
		private List<Sonar> _irSensors = new();
		// private List<SensorDevices.Magnet> magnetSensors = null;
		private List<Contact> _bumperSensors = new();

		private float _accumulatedTime = 0f;

		private StringBuilder _log = new();

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
			_motorControl = motorControl;
		}

		public void SetIMU(in string sensorName)
		{
			var imuList = gameObject.GetComponentsInChildren<IMU>();
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
			var sonarList = GetComponentsInChildren<Sonar>();

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
			var sonarList = GetComponentsInChildren<Sonar>();

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
			var modelList = GetComponentsInChildren<SDFormat.Helper.Model>();
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
			_log.AppendLine("Magnet is to be implemented");
		}

		public void SetBumper(in List<string> bumperList)
		{
			var contactList = GetComponentsInChildren<Contact>();

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

		public void SetBattery(in Battery targetBattery)
		{
			_batteryData = new messages.Battery();

			_battery = targetBattery;
			_batteryData.Name = _battery.Name;
		}

		protected override void OnReset()
		{
			_imuSensor?.Reset();
		}

		protected override void InitializeMessages()
		{
			_odomData = new messages.Micom.Odometry
			{
				AngularVelocity = new messages.Micom.Odometry.Wheel(),
				LinearVelocity = new messages.Micom.Odometry.Wheel(),
				Pose = new messages.Vector3d(),
				Twist = new messages.Twist
				{
					Linear = new messages.Vector3d(),
					Angular = new messages.Vector3d()
				}
			};
		}

		void FixedUpdate()
		{
			if (_motorControl?.Update(_odomData, Time.fixedDeltaTime, _imuSensor) == false)
			{
				Debug.LogWarning("Update failed in MotorControl");
			}

			// Skip message generation until UpdateRate is configured
			if (UpdateRate <= 0)
				return;

			_accumulatedTime += Time.fixedDeltaTime;

			if (_accumulatedTime < UpdatePeriod)
				return;

			// Clamp to avoid runaway accumulation (e.g. after a long pause)
			_accumulatedTime = _accumulatedTime % UpdatePeriod;

			// Always allocate a fresh message: IsEmpty only means the queue was
			// drained, not that the TX thread finished serializing the dequeued
			// object. Reusing the same instance causes the TX thread to publish
			// the mutated (next-frame) timestamp twice → duplicate timestamps.
			var micomSensorData = new messages.Micom { Time = new messages.Time() };

			micomSensorData.Bumpers.Clear();
			micomSensorData.Usses.Clear();
			micomSensorData.Irs.Clear();
			micomSensorData.Time.Set(GetNextSyntheticTime());

			UpdateBattery(micomSensorData, UpdatePeriod);
			UpdateUss(micomSensorData);
			UpdateIr(micomSensorData);
			UpdateBumper(micomSensorData);

			micomSensorData.Odom = _odomData;

			EnqueueMessage(micomSensorData);

#if UNITY_EDITOR
			UpdateProfiler("MicomSensor", 512);
#endif
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
					if (index >= _bumperMessages.Count)
						_bumperMessages.Add(new messages.Micom.Bumper());
					var bumper = _bumperMessages[index];
					bumper.Bumped = bumperSensor.IsContacted();
					bumper.Contacts = bumperSensor.GetContacts();
					micomData.Bumpers.Add(bumper);
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
					if (index >= _ussMessages.Count)
						_ussMessages.Add(new messages.Micom.Uss());
					var uss = _ussMessages[index];
					uss.Distance = ussSensor.GetDetectedRange();
					uss.State = ussSensor.GetSonar();
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
					var irSensor = _irSensors[index];
					if (index >= _irMessages.Count)
						_irMessages.Add(new messages.Micom.Ir());
					var ir = _irMessages[index];
					ir.Distance = irSensor.GetDetectedRange();
					ir.State = irSensor.GetSonar();
					micomData.Irs.Add(ir);
				}
			}
		}
	}
}