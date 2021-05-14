/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;

public partial class MicomSensor : Device
{
	private messages.Micom micomSensorData = null;

	private SensorDevices.IMU imuSensor = null;
	private List<SensorDevices.Sonar> ussSensors = new List<SensorDevices.Sonar>();
	private List<SensorDevices.Sonar> irSensors = new List<SensorDevices.Sonar>();
	// private List<SensorDevices.Magnet> magnetSensors = null;

	private SensorDevices.Contact bumperContact = null;
	private List<ArticulationBody> bumperSensors = new List<ArticulationBody>();

	private Dictionary<string, Pose> partsPoseMapTable = new Dictionary<string, Pose>();

	protected override void OnAwake()
	{
		Mode = ModeType.TX_THREAD;
		DeviceName = "MicomSensor";
	}

	protected override void OnStart()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		SetUpdateRate(updateRate);

		pidGainP = GetPluginParameters().GetValue<float>("PID/kp");
		pidGainI = GetPluginParameters().GetValue<float>("PID/ki");
		pidGainD = GetPluginParameters().GetValue<float>("PID/kd");

		wheelTread = GetPluginParameters().GetValue<float>("wheel/tread");
		wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");
		divideWheelRadius = 1.0f/wheelRadius;

		wheelNameLeft = GetPluginParameters().GetValue<string>("wheel/location[@type='left']");
		wheelNameRight = GetPluginParameters().GetValue<string>("wheel/location[@type='right']");

		var motorFriction = GetPluginParameters().GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = GetPluginParameters().GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		var modelList = GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var model in modelList)
		{
			// Debug.Log(model.name);
			if (model.name.Equals(wheelNameLeft) || model.name.Equals(wheelNameRight))
			{
				var motorObject = model.gameObject;
				var motor = gameObject.AddComponent<Motor>();
				motor.SetTargetJoint(motorObject);
				motor.SetPID(pidGainP, pidGainI, pidGainD);
				_motors.Add(model.name, motor);

				SetInitialPartsPose(model.name, motorObject);
			}
		}

		if (GetPluginParameters().GetValues<string>("uss/sensor", out var ussList))
		{
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

		if (GetPluginParameters().GetValues<string>("ir/sensor", out var irList))
		{
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

		if (GetPluginParameters().GetValues<string>("magnet/sensor", out var magnetList))
		{
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
		}

		var targetContactName = GetPluginParameters().GetAttribute<string>("bumper", "contact");
		// Debug.Log(targetContactName);

		var contactsInChild = GetComponentsInChildren<SensorDevices.Contact>();

		foreach (var contact in contactsInChild)
		{
			if (contact.name.Equals(targetContactName))
			{
				bumperContact = contact;
				// Debug.Log("Found");
			}
		}

		if (bumperContact != null)
		{
			if (GetPluginParameters().GetValues<string>("bumper/joint_name", out var bumperJointNameList))
			{
				var linkList = GetComponentsInChildren<SDF.Helper.Link>();
				foreach (var link in linkList)
				{
					foreach (var bumperJointName in bumperJointNameList)
					{
						if (link.jointList.TryGetValue(bumperJointName, out var articulationBody))
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
			}

			var bumperCount = (bumperSensors == null || bumperSensors.Count == 0) ? 1 : bumperSensors.Count;

			micomSensorData.bumper.Bumpeds = new bool[bumperCount];
		}

		imuSensor = gameObject.GetComponentInChildren<SensorDevices.IMU>();

		if (imuSensor != null)
		{
			SetInitialPartsPose(imuSensor.name, imuSensor.gameObject);
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
		foreach (var motor in _motors.Values)
		{
			motor.GetPID().Change(pidGainP, pidGainI, pidGainD);
		}

		UpdateIMU();
		UpdateUss();
		UpdateIr();
		UpdateBumper();
		UpdateOdom(Time.fixedDeltaTime);
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
				var index = 0;
				foreach (var bumperBody in bumperSensors)
				{
					// TODO:
					// var articulationDrive = (bumperBody.xDrive != null)? bumper.

					// bumper.xDrive.upperLimit
					// var threshold = bumperBody.linearLimit.limit/2;

					var normal = bumperBody.transform.localPosition.normalized;
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

					index++;
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

		var index = 0;
		foreach (var uss in ussSensors)
		{
			micomSensorData.uss.Distances[index++] = uss.GetDetectedRange();
		}
	}

	private void UpdateIr()
	{
		if ((micomSensorData == null || micomSensorData.ir == null))
		{
			return;
		}

		var index = 0;
		foreach (var ir in irSensors)
		{
			micomSensorData.ir.Distances[index++] = ir.GetDetectedRange();
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

	private void SetInitialPartsPose(in string name, in GameObject targetObject)
	{
		var targetTransform = (targetObject.CompareTag("Model")) ? targetObject.transform : targetObject.transform.parent;
		var initialPose = new Pose(targetTransform.localPosition, targetTransform.localRotation);
		// Debug.Log(name + " " + initialPose.ToString("F9"));
		partsPoseMapTable.Add(name, initialPose);
	}

	public Pose GetPartsPose(in string targetPartsName)
	{
		if (partsPoseMapTable.TryGetValue(targetPartsName, out var targetPartsPose))
		{
			return targetPartsPose;
		}

		return Pose.identity;
	}
}