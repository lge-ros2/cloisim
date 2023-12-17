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

		private SensorDevices.IMU imuSensor = null;
		private List<SensorDevices.Sonar> ussSensors = new List<SensorDevices.Sonar>();
		private List<SensorDevices.Sonar> irSensors = new List<SensorDevices.Sonar>();
		// private List<SensorDevices.Magnet> magnetSensors = null;

		private SensorDevices.Contact bumperContact = null;
		private List<ArticulationBody> bumperSensors = new List<ArticulationBody>();

		private SensorDevices.Battery battery = null;

		private MotorControl _motorControl = new MotorControl();
		public MotorControl MotorControl => this._motorControl;

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

		public void SetIMU(in string name)
		{
			var imuList = gameObject.GetComponentsInChildren<SensorDevices.IMU>();
			foreach (var imu in imuList)
			{
				if (imu.DeviceName.Contains("::" + name + "::"))
				{
					Debug.Log(imu.DeviceName + " attached to Micom");
					imuSensor = imu;
					break;
				}
			}
		}

		public void SetWheel(in string wheelNameLeft, in string wheelNameRight)
		{
			var linkList = GetComponentsInChildren<SDF.Helper.Link>();
			foreach (var link in linkList)
			{
				var wheelLocation = MotorControl.WheelLocation.NONE;

				if (link.name.Equals(wheelNameLeft) || link.Model.name.Equals(wheelNameLeft))
				{
					wheelLocation = MotorControl.WheelLocation.LEFT;

				}
				else if (link.name.Equals(wheelNameRight) || link.Model.name.Equals(wheelNameRight))
				{
					wheelLocation = MotorControl.WheelLocation.RIGHT;
				}
				else
				{
					continue;
				}

				if (!wheelLocation.Equals(MotorControl.WheelLocation.NONE))
				{
					var motorObject = (link.gameObject != null) ? link.gameObject : link.Model.gameObject;
					_motorControl.AttachWheel(wheelLocation, motorObject);
				}
			}
		}

		public void SetWheel(in string frontWheelLeftName, in string frontWheelRightName, in string rearWheelLeftName, in string rearWheelRightName)
		{
			SetWheel(frontWheelLeftName, frontWheelRightName);

			var linkList = GetComponentsInChildren<SDF.Helper.Link>();
			foreach (var link in linkList)
			{
				var wheelLocation = MotorControl.WheelLocation.NONE;

				if (link.name.Equals(rearWheelLeftName) || link.Model.name.Equals(rearWheelLeftName))
				{
					wheelLocation = MotorControl.WheelLocation.REAR_LEFT;

				}
				else if (link.name.Equals(rearWheelRightName) || link.Model.name.Equals(rearWheelRightName))
				{
					wheelLocation = MotorControl.WheelLocation.REAR_RIGHT;
				}
				else
				{
					continue;
				}

				if (!wheelLocation.Equals(MotorControl.WheelLocation.NONE))
				{
					var motorObject = (link.gameObject != null) ? link.gameObject : link.Model.gameObject;
					_motorControl.AttachWheel(wheelLocation, motorObject);
				}
			}
		}

		public void SetMotorConfiguration(in float wheelRadius, in float wheelSeparation, in float P, in float I, in float D)
		{
			_motorControl.SetPID(P, I, D);
			_motorControl.SetWheelInfo(wheelRadius, wheelSeparation);
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

		public void SetBumper(in string contactName)
		{
			// Debug.Log(targetContactName);
			var contactsInChild = GetComponentsInChildren<SensorDevices.Contact>();

			foreach (var contact in contactsInChild)
			{
				if (contact.name.Equals(contactName))
				{
					bumperContact = contact;
					// Debug.Log("Found");
				}
			}
		}

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

			if (_motorControl != null)
			{
				_motorControl.Reset();
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
			if (_motorControl == null || micomSensorData == null)
			{
				Debug.LogWarning("micomSensorData or motorControl is NULL");
				return;
			}

			var deltaTime = Time.fixedDeltaTime;

			if (battery != null)
			{
				micomSensorData.Battery.Voltage = battery.Update(deltaTime);
			}

			if (_motorControl.Update(micomSensorData.Odom, deltaTime, imuSensor) == false)
			{
				Debug.LogWarning("Update failed in MotorControl");
			}

			UpdateIMU();
			UpdateUss();
			UpdateIr();
			UpdateBumper();

			DeviceHelper.SetTime(micomSensorData.Time, DeviceHelper.GlobalClock.FixedSimTime);
		}

		private void UpdateBumper()
		{
			if (micomSensorData == null || micomSensorData.bumper == null || bumperContact == null)
			{
				return;
			}

			if (bumperContact.IsContacted())
			{
				if (bumperSensors == null || bumperSensors.Count == 0)
				{
					micomSensorData.bumper.Bumpeds[0] = true;
				}
				else
				{
					for (var index = 0; index < bumperSensors.Count; index++)
					{
						// TODO: to be implemented

						// var normal = bumperSensors[index].transform.localPosition.normalized;
						// // Debug.Log(index + ": " + normal.ToString("F6"));

						// if (normal.x > 0 && normal.z < 0)
						// {
						// 	micomSensorData.bumper.Bumpeds[index] = true;
						// 	// Debug.Log("Left Bumped");
						// }
						// else if (normal.x < 0 && normal.z < 0)
						// {
						// 	micomSensorData.bumper.Bumpeds[index] = true;
						// 	// Debug.Log("Right Bumped");
						// }
						// else
						// {
						// 	micomSensorData.bumper.Bumpeds[index] = false;
						// 	// Debug.Log("No Bumped");
						// }
					}
				}
			}
		}

		private void UpdateUss()
		{
			if ((micomSensorData == null || micomSensorData.uss == null))
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
			if ((micomSensorData == null || micomSensorData.ir == null))
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