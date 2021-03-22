/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;

public partial class MicomSensor : Device
{
	private messages.Micom micomSensorData = null;

#region Motor Related
	private string _wheelNameLeft = string.Empty;
	private string _wheelNameRight = string.Empty;

	private Dictionary<string, Motor> _motors = new Dictionary<string, Motor>();

	public float _PGain, _IGain, _DGain;

	private float wheelBase = 0.0f;
	private float wheelRadius = 0.0f;
	private float divideWheelRadius = 0.0f; // for computational performance
#endregion

	private SensorDevices.IMU imuSensor = null;
	private List<SensorDevices.Sonar> ussSensors = new List<SensorDevices.Sonar>();
	private List<SensorDevices.Sonar> irSensors = new List<SensorDevices.Sonar>();
	// private List<SensorDevices.Magnet> magnetSensors = null;

	private SensorDevices.Contact bumperContact = null;
	private List<ArticulationBody> bumperSensors = new List<ArticulationBody>();

	private Dictionary<string, Pose> partsPoseMapTable = new Dictionary<string, Pose>();

	public float WheelBase => wheelBase;
	public float WheelRadius => wheelRadius;

	protected override void OnAwake()
	{
		deviceName = "MicomSensor";
	}

	protected override void OnStart()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		SetUpdateRate(updateRate);

		_PGain = GetPluginParameters().GetValue<float>("PID/kp");
		_IGain = GetPluginParameters().GetValue<float>("PID/ki");
		_DGain = GetPluginParameters().GetValue<float>("PID/kd");

		wheelBase = GetPluginParameters().GetValue<float>("wheel/base");
		wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");
		divideWheelRadius = 1.0f/wheelRadius;

		_wheelNameLeft = GetPluginParameters().GetValue<string>("wheel/location[@type='left']");
		_wheelNameRight = GetPluginParameters().GetValue<string>("wheel/location[@type='right']");

		var motorFriction = GetPluginParameters().GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = GetPluginParameters().GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		var modelList = GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var model in modelList)
		{
			// Debug.Log(model.name);
			if (model.name.Equals(_wheelNameLeft) || model.name.Equals(_wheelNameRight))
			{
				var motorObject = model.gameObject;
				var motor = gameObject.AddComponent<Motor>();
				motor.SetTargetJoint(motorObject);
				motor.SetPID(_PGain, _IGain, _DGain);
				_motors.Add(model.name, motor);

				SetPartsInitialPose(model.name, motorObject);
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
			SetPartsInitialPose(imuSensor.name, imuSensor.gameObject);
		}
	}

	protected override IEnumerator DeviceCoroutine()
	{
		var sw = new Stopwatch();
		while (true)
		{
			sw.Restart();
			GenerateMessage();
			sw.Stop();
			yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
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
		PushData<messages.Micom>(micomSensorData);
	}

	void FixedUpdate()
	{
		foreach (var motor in _motors.Values)
		{
			motor.GetPID().Change(_PGain, _IGain, _DGain);
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

	public bool UpdateOdom(in float duration)
	{
		if (micomSensorData != null)
		{
			var odom = micomSensorData.Odom;
			if ((odom != null))
			{
				var motorLeft = _motors[_wheelNameLeft];
				var motorRight = _motors[_wheelNameRight];

				if (motorLeft == null || motorRight == null)
				{
					Debug.Log("cannot find motor object");
					return false;
				}

				var angularVelocityLeft = motorLeft.GetCurrentVelocity();
				var angularVelocityRight = motorRight.GetCurrentVelocity();

				// Set reversed value due to different direction
				// Left-handed -> Right-handed direction of rotation
				odom.AngularVelocity.Left = -angularVelocityLeft * Mathf.Deg2Rad;
				odom.AngularVelocity.Right = -angularVelocityRight * Mathf.Deg2Rad;
				odom.LinearVelocity.Left = odom.AngularVelocity.Left * wheelRadius;
				odom.LinearVelocity.Right = odom.AngularVelocity.Right * wheelRadius;

				if (imuSensor != null)
				{
					var imuOrientation = imuSensor.GetOrientation();
					var yaw = imuOrientation.y * Mathf.Deg2Rad;
					CalculateOdometry(duration, (float)odom.AngularVelocity.Left, (float)odom.AngularVelocity.Right, yaw);
				}

				// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
				odom.Pose.X = _odomPose.x;
				odom.Pose.Y = -_odomPose.y;
				odom.Pose.Z = -_odomPose.z;

				odom.TwistLinear.X = _odomVelocity.x;

				// Set reversed value due to different direction (Left-handed -> Right-handed direction of rotation)
				odom.TwistAngular.Z = -_odomVelocity.y;

				motorLeft.Feedback.SetRotatingVelocity(_odomVelocity.y);
				motorRight.Feedback.SetRotatingVelocity(_odomVelocity.y);
				// Debug.LogFormat("jointvel: {0}, {1}", angularVelocityLeft * Mathf.Deg2Rad, angularVelocityRight * Mathf.Deg2Rad);
				// Debug.LogFormat("Odom: {0}, {1}", odom.AngularVelocity.Left, odom.AngularVelocity.Right);

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

	public void UpdateMotorFeedback(in float linearVelocityLeft, in float linearVelocityRight)
	{
		var linearVelocity = (linearVelocityLeft + linearVelocityRight) * 0.5f;
		var angularVelocity = (linearVelocityRight - linearVelocity) / (wheelBase * 0.5f);

		UpdateMotorFeedback(angularVelocity);
	}

	public void UpdateMotorFeedback(in float angularVelocity)
	{
		foreach (var motor in _motors.Values)
		{
			motor.Feedback.SetRotatingTargetVelocity(angularVelocity);
		}
	}

	/// <summary>Set motor velocity</summary>
	/// <remarks>degree per second</remarks>
	private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
	{
		var isRotating = (Mathf.Sign(angularVelocityLeft) != Mathf.Sign(angularVelocityRight));

		foreach (var motor in _motors.Values)
		{
			motor.Feedback.SetMotionRotating(isRotating);
		}

		var motorLeft = _motors[_wheelNameLeft];
		var motorRight = _motors[_wheelNameRight];

		if (motorLeft != null)
		{
			motorLeft.SetVelocityTarget(angularVelocityLeft);
		}

		if (motorRight != null)
		{
			motorRight.SetVelocityTarget(angularVelocityRight);
		}
	}

	private void SetPartsInitialPose(in string name, in GameObject targetObject)
	{
		var targetParentTransform = targetObject.transform.parent;
		var initialPose = new Pose(targetParentTransform.localPosition, targetParentTransform.localRotation);
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