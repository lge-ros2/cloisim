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

		private MotorControl motorControl = new MotorControl();
		public MotorControl MotorControl => this.motorControl;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "MicomSensor";
		}

		protected override void OnStart()
		{
			imuSensor = gameObject.GetComponentInChildren<SensorDevices.IMU>();
		}

		protected override IEnumerator OnVisualize()
		{
			yield return null;
		}

		public void SetWheel(in string wheelNameLeft, in string wheelNameRight)
		{
			var modelList = GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var model in modelList)
			{
				var wheelLocation = MotorControl.WheelLocation.NONE;

				if (model.name.Equals(wheelNameLeft))
				{
					wheelLocation = MotorControl.WheelLocation.LEFT;
				}
				else if (model.name.Equals(wheelNameRight))
				{
					wheelLocation = MotorControl.WheelLocation.RIGHT;
				}
				else
				{
					continue;
				}

				var motorObject = model.gameObject;
				motorControl.AddWheelInfo(wheelLocation, motorObject);
				// Debug.Log(model.name);
			}
		}

		public void SetMotorConfiguration(in float wheelRadius, in float wheelTread, in float P, in float I, in float D)
		{
			motorControl.SetPID(P, I, D);
			motorControl.SetWheelInfo(wheelRadius, wheelTread);
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
						if (link.GetJointInfo(bumperJointName, out var axisInfo, out var articulationBody))
						{
							if (articulationBody.jointType == ArticulationJointType.PrismaticJoint)
							{
								bumperSensors.Add(articulationBody);
								Debug.Log(bumperJointName);
							}
							else
							{
								Debug.Log(bumperJointName + " is not a prismatic joint type!!!");
							}
						}
					}
				}

				var bumperCount = bumperSensors.Count;
				micomSensorData.bumper.Bumpeds = new bool[bumperCount];
			}
		}

		protected override void OnReset()
		{
			if (imuSensor != null)
			{
				imuSensor.Reset();
			}

			if (motorControl != null)
			{
				motorControl.Reset();
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
			DeviceHelper.SetCurrentTime(micomSensorData.Time);
			PushDeviceMessage<messages.Micom>(micomSensorData);
		}

		void FixedUpdate()
		{
			var motorLeft = motorControl.GetMotor(MotorControl.WheelLocation.LEFT);
			var motorRight = motorControl.GetMotor(MotorControl.WheelLocation.RIGHT);

			if (motorLeft != null && motorRight != null)
			{
				motorLeft.Update();
				motorRight.Update();
			}

			UpdateIMU();
			UpdateUss();
			UpdateIr();
			UpdateBumper();

			motorControl.UpdateOdometry(micomSensorData.Odom, Time.fixedDeltaTime, imuSensor);
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
						// TODO:
						// var articulationDrive = (bumperBody.xDrive != null)? bumper.

						// bumper.xDrive.upperLimit
						// var threshold = bumperBody.linearLimit.limit/2;

						var normal = bumperSensors[index].transform.localPosition.normalized;
						// Debug.Log(index + ": " + normal.ToString("F6"));

						if (normal.x > 0 && normal.z < 0)
						{
							micomSensorData.bumper.Bumpeds[index] = true;
							// Debug.Log("Left Bumped");
						}
						else if (normal.x < 0 && normal.z < 0)
						{
							micomSensorData.bumper.Bumpeds[index] = true;
							// Debug.Log("Right Bumped");
						}
						else
						{
							micomSensorData.bumper.Bumpeds[index] = false;
							// Debug.Log("No Bumped");
						}
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