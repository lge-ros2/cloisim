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
		private SensorDevices.IMU imuSensor = null;
		private List<SensorDevices.Sonar> ussSensors = new List<SensorDevices.Sonar>();
		private List<SensorDevices.Sonar> irSensors = new List<SensorDevices.Sonar>();
		// private List<SensorDevices.Magnet> magnetSensors = null;
		private List<SensorDevices.Contact> bumperSensors = new List<SensorDevices.Contact>();

		// private List<ArticulationBody> bumperSensors = new List<ArticulationBody>();


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
				// Debug.Log(imu.name + " , " + imu.DeviceName);
				if (imu.DeviceName.Contains("::" + sensorName + "::") ||
					imu.name.CompareTo(sensorName) == 0)
				{
					Debug.Log(imu.DeviceName + " attached to Micom");
					imuSensor = imu;
					break;
				}
			}
		}

		public void SetUSS(in List<string> ussList)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var uss in ussList)
			{
				foreach (var model in modelList)
				{
					if (model.name.Equals(uss))
					{
						var sonarSensor = model.GetComponentInChildren<SensorDevices.Sonar>();
						ussSensors.Add(sonarSensor);
						// Debug.Log("ussSensor found : " + sonarSensor.name);
					}
				}
			}
			micomSensorData.uss.Distances = new double[ussList.Count];
		}

		public void SetIRSensor(in List<string> irList)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var ir in irList)
			{
				foreach (var model in modelList)
				{
					if (model.name.Equals(ir))
					{
						var sonarSensor = model.GetComponentInChildren<SensorDevices.Sonar>();
						irSensors.Add(sonarSensor);
						// Debug.Log("irSensor found : " + sonarSensor.name);
					}
				}
			}
			micomSensorData.ir.Distances = new double[irList.Count];
		}

		public void SetMagnet(in List<string> magnetList)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
		}

		public void SetBumper(in List<string> bumperList)
		{
			// Debug.Log(targetContactName);
			var contactsInChild = GetComponentsInChildren<SensorDevices.Contact>();
			foreach (var bumper in bumperList)
			{
				foreach (var contact in contactsInChild)
				{
					if (contact.name.Equals(bumper))
					{
						bumperSensors.Add(contact);
						Debug.Log("Found " + contact.name);
					}
				}
			}

			var bumperCount = bumperSensors.Count;
			micomSensorData.bumper.Bumpeds = new bool[bumperCount];
		}

#if false
		public void SetBumperSensor(in List<string> bumperJointNameList)
		{
			if (bumperContact != null)
			{
				var linkList = GetComponentsInChildren<SDF.Helper.Link>();
				foreach (var link in linkList)
				{
					foreach (var bumperJointName in bumperJointNameList)
					{
						// TODO: to be implemented
					}
				}

				var bumperCount = bumperSensors.Count;
				micomSensorData.bumper.Bumpeds = new bool[bumperCount];
			}
		}
#endif

		public void SetBattery(in SensorDevices.Battery targetBattery)
		{
			micomSensorData.Battery = new messages.Battery();

			this.battery = targetBattery;
			micomSensorData.Battery.Name = battery.Name;
		}

		protected override void OnReset()
		{
			if (imuSensor != null)
			{
				imuSensor.Reset();
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
				if (_motorControl.Update(micomSensorData.Odom, deltaTime, imuSensor) == false)
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

			for (var index = 0; index < bumperSensors.Count; index++)
			{
				var bumperSensor = bumperSensors[index];
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

			for (var index = 0; index < ussSensors.Count; index++)
			{
				micomSensorData.uss.Distances[index] = ussSensors[index].GetDetectedRange();
			}
		}

		private void UpdateIr()
		{
			if (micomSensorData == null || micomSensorData.ir == null)
			{
				return;
			}

			for (var index = 0; index < irSensors.Count; index++)
			{
				micomSensorData.ir.Distances[index] = irSensors[index].GetDetectedRange();
			}
		}

		private void UpdateIMU()
		{
			if (imuSensor == null || micomSensorData == null)
			{
				return;
			}

			micomSensorData.Imu = imuSensor.GetImuMessage();
		}
	}
}