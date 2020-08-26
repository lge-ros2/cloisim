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

	private SensorDevices.IMU imuSensor = null;
	private List<SensorDevices.Sonar> ussSensors = new List<SensorDevices.Sonar>();
	private List<SensorDevices.Sonar> irSensors = new List<SensorDevices.Sonar>();
	// private List<SensorDevices.Magnet> magnetSensors = null;

	private SensorDevices.Contact bumperContact = null;
	private List<ConfigurableJoint> bumperSensors = new List<ConfigurableJoint>();


	private float wheelBase = 0.0f;
	private float wheelRadius = 0.0f;
	private float divideWheelRadius = 0.0f; // for computational performacne.


	public float WheelBase => wheelBase;
	public float WheelRadius => wheelRadius;

	private Dictionary<string, Pose> partsPoseMapTable = new Dictionary<string, Pose>();

	protected override void OnAwake()
	{
		deviceName = "MicomSensor";
	}

	protected override void OnStart()
	{
		var updateRate = parameters.GetValue<float>("update_rate", 20);
		SetUpdateRate(updateRate);

		var kp = parameters.GetValue<float>("PID/kp");
		var ki = parameters.GetValue<float>("PID/ki");
		var kd = parameters.GetValue<float>("PID/kd");

		var pidControl = new PID(kp, ki, kd);

		wheelBase = parameters.GetValue<float>("wheel/base");
		wheelRadius = parameters.GetValue<float>("wheel/radius");
		divideWheelRadius = 1.0f/wheelRadius;

		var wheelNameLeft = parameters.GetValue<string>("wheel/location[@type='left']");
		var wheelNameRight = parameters.GetValue<string>("wheel/location[@type='right']");

		var motorFriction = parameters.GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = parameters.GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		var modelList = GetComponentsInChildren<ModelPlugin>();
		foreach (var model in modelList)
		{
			// Debug.Log(model.name);
			if (model.name.Equals(wheelNameLeft))
			{
				var jointWheelLeft = model.GetComponentInChildren<HingeJoint>();
				motorLeft = new Motor("Left", jointWheelLeft, pidControl);

				// var wheelLeftBody = jointWheelLeft.gameObject.GetComponent<Rigidbody>();
				var wheelLeftTransform = jointWheelLeft.gameObject.transform.parent;
				var wheelLeftPose = new Pose(wheelLeftTransform.localPosition, wheelLeftTransform.localRotation);
				partsPoseMapTable.Add(wheelNameLeft, wheelLeftPose);

				// Debug.LogFormat("joint Wheel({0}) Left max angular velocity({1})", jointWheelLeft.name, jointWheelLeft.gameObject.GetComponent<Rigidbody>().maxAngularVelocity);
			}
			else if (model.name.Equals(wheelNameRight))
			{
				var jointWheelRight = model.GetComponentInChildren<HingeJoint>();
				motorRight = new Motor("Right", jointWheelRight, pidControl);

				var wheelRightTransform = jointWheelRight.gameObject.transform.parent;
				var wheelRightPose = new Pose(wheelRightTransform.localPosition, wheelRightTransform.localRotation);
				partsPoseMapTable.Add(wheelNameRight, wheelRightPose);

				// Debug.LogFormat("joint Wheel({0}) Left max angular velocity({1})", jointWheelRight.name, jointWheelRight.gameObject.GetComponent<Rigidbody>().maxAngularVelocity);
			}

			if (motorLeft != null && motorRight != null)
			{
				break;
			}
		}

		if (parameters.GetValues<string>("uss/sensor", out var ussList))
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

		if (parameters.GetValues<string>("ir/sensor", out var irList))
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

		if (parameters.GetValues<string>("magnet/sensor", out var magnetList))
		{
			foreach (var model in modelList)
			{
				// TODO: to be implemented
			}
		}

		var targetContactName = parameters.GetAttribute<string>("bumper", "contact");
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
			if (parameters.GetValues<string>("bumper/joint_name", out var bumperJointNameList))
			{
				var linkList = GetComponentsInChildren<LinkPlugin>();
				foreach (var link in linkList)
				{
					foreach (var bumperJointName in bumperJointNameList)
					{
						if (link.jointList.TryGetValue(bumperJointName, out var jointValue))
						{
							bumperSensors.Add(jointValue as ConfigurableJoint);
							Debug.Log(bumperJointName);
						}
					}
				}
			}

			var bumperCount = (bumperSensors == null || bumperSensors.Count == 0) ? 1 : bumperSensors.Count;

			micomSensorData.bumper.Bumpeds = new bool[bumperCount];
		}

		imuSensor = gameObject.GetComponentInChildren<SensorDevices.IMU>();
		var imuSensorModelTransform = imuSensor.transform.parent;
		var newPose = new Pose(imuSensorModelTransform.localPosition, imuSensorModelTransform.localRotation);
		partsPoseMapTable.Add(imuSensor.name, newPose);
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
		micomSensorData.Accgyro = new messages.Micom.AccGyro();
		micomSensorData.Odom = new messages.Micom.Odometry();
		micomSensorData.uss = new messages.Micom.Uss();
		micomSensorData.ir = new messages.Micom.Ir();
		micomSensorData.bumper = new messages.Micom.Bumper();
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
		UpdateBumper();
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
				foreach (var bumperJoint in bumperSensors)
				{
					var threshold = bumperJoint.linearLimit.limit/2;

					var normal = bumperJoint.transform.localPosition.normalized;
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
		var imu = micomSensorData.Imu;

		if (imuSensor == null || micomSensorData == null)
		{
			return;
		}

		micomSensorData.Imu = imuSensor.GetImuMessage();
	}

	private void UpdateAccGyro()
	{
		var accGyro = micomSensorData.Accgyro;

		if (micomSensorData == null || accGyro == null)
		{
			return;
		}

		var localRotation = transform.rotation;
		var angle = localRotation.eulerAngles * Mathf.Deg2Rad;

		accGyro.AngleX = angle.x;
		accGyro.AngleY = angle.z;
		accGyro.AngleZ = angle.y;

		accGyro.AccX = 0;
		accGyro.AccY = 0;
		accGyro.AccZ = 0;

		accGyro.AngulerRateX = 0;
		accGyro.AngulerRateY = 0;
		accGyro.AngulerRateZ = 0;
	}

	private bool SetOdomData(in float angularVelocityLeft, in float angularVelocityRight)
	{
		if (micomSensorData != null)
		{
			var odom = micomSensorData.Odom;

			if (odom != null)
			{
				odom.AngularVelocityLeft = angularVelocityLeft * Mathf.Deg2Rad;
				odom.AngularVelocityRight = angularVelocityRight * Mathf.Deg2Rad;
				odom.LinearVelocityLeft = odom.AngularVelocityLeft * wheelRadius;
				odom.LinearVelocityRight = odom.AngularVelocityRight * wheelRadius;
				// Debug.LogFormat("Odom {0}, {1} ", odom.SpeedLeft, odom.SpeedRight);
				return true;
			}
		}

		return false;
	}


	/// <summary>Set differential driver</summary>
	/// <remarks>rad per second for wheels</remarks>
	public void SetDifferentialDrive(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var angularVelocityLeft = linearVelocityLeft * divideWheelRadius * Mathf.Rad2Deg;
		var angularVelocityRight = linearVelocityRight * divideWheelRadius * Mathf.Rad2Deg;

		SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
	}

	public void SetTwistDrive(in float linearVelocity, in float angularVelocity)
	{
		// m/s, rad/s
		// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * wheelBase)) / (2 * wheelRadius);
		// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * wheelBase)) / (2 * wheelRadius);
		var angularCalculation = (angularVelocity * wheelBase * 0.5f);
		var linearVelocityLeft = (linearVelocity - angularCalculation);
		var linearVelocityRight = (linearVelocity + angularCalculation);

		SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
	}


	/// <summary>Set motor velocity</summary>
	/// <remarks>degree per second</remarks>
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		if (motorLeft != null && motorRight != null)
		{
			motorLeft.SetVelocityTarget(angularVelocityLeft);
			motorRight.SetVelocityTarget(angularVelocityRight);

			var angularVelocityLeftFromMotor = motorLeft.GetCurrentVelocity();
			var angularVelocityRightFromMotor = motorRight.GetCurrentVelocity();

			SetOdomData(angularVelocityLeftFromMotor, angularVelocityRightFromMotor);
		}
	}

	public void SetPluginParameter(in PluginParameters pluginParams)
	{
		parameters = pluginParams;
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