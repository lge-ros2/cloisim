/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using SelfBalanceControl;
using UnityEngine;
using messages = cloisim.msgs;

public class BalancedDrive : MotorControl
{
	private readonly float WHEEL_MAX_TORQUE = 10;
	private readonly float WHEEL_MAX_SPEED = 60;

	private readonly float HIP_MAX_TORQUE = 10;
	private readonly float HIP_MAX_SPEED = 30;

	private Kinematics _kinematics = null;
	private ExpProfiler _pitchProfiler;
	private SlidingModeControl _smc = null;

	public bool _onBalancing = false;
	private double _commandTwistLinear = 0;
	private double _commandTwistAngular = 0;
	private double _commandPitch = 0;
	private double _commandHeadsetTarget = 0;
	private Vector2d _commandHipTarget = Vector2d.zero;

	public double PitchTarget
	{
		get => _commandPitch;
		set {
			_commandPitch = value;
			_pitchProfiler.Reset(Time.timeAsDouble, _commandPitch, 0);
		}
	}

	public double HeadsetTarget
	{
		get => _commandHeadsetTarget;
		set => _commandHeadsetTarget = value;
	}

	public Vector2d HipTarget
	{
		get => _commandHipTarget;
		set => _commandHipTarget = value;
	}

	public BalancedDrive(
		in Transform controllerTransform,
		in SlidingModeControl.OutputMode outputMode,
		in SlidingModeControl.SwitchingMode switchingMode,
		in double expProfilerTimeConstant)
		: base(controllerTransform)
	{
		_pitchProfiler = new ExpProfiler(expProfilerTimeConstant);
		_smc = new SlidingModeControl(outputMode, switchingMode);
	}

	public BalancedDrive(
		in Transform controllerTransform,
		in string outputMode = "LQR",
		in string switchingMode = "SAT",
		in double expProfilerTimeConstant = 5)
		: this(controllerTransform,
			SlidingModeControl.ParseOutputMode(outputMode),
			SlidingModeControl.ParseSwitchingMode(switchingMode),
			expProfilerTimeConstant)
	{
	}

	public override void Reset()
	{
		Debug.Log("BalancedDrive Reset ========================");

		foreach (var wheel in _motorList)
		{
			wheel.Value?.Reset();
		}

		var wheelVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var wheelVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);
		var pitch = 0;
		_kinematics.Reset(wheelVelocityLeft, wheelVelocityRight, pitch);
		_smc.Reset();
		PitchTarget = 0.000f;

		_commandHeadsetTarget = 0f;
		SetEfforts(VectorXd.Zero(5));
	}

	public override void SetWheelInfo(in float radius, in float separation)
	{
		_kinematics = new Kinematics(radius, separation);
	}

	public void SetSMCParams(in double K_sw, in double sigma_b, in double ff)
	{
		if (_smc != null)
		{
			_smc.SetParams(K_sw, sigma_b, ff);
		}
	}

	public void SetSMCNominalModel(in string matrixA, in string matrixB, in string matrixK, in string matrixS)
	{
		if (_smc != null)
		{
			_smc.SetNominalModel(matrixA, matrixB, matrixK, matrixS);
		}
	}

	public void SetHipJointPID(
		float p, float i, float d,
		float integralMin, float integralMax,
		float outputMin, float outputMax)
	{
		if (_motorList[Location.HIP_LEFT] != null)
			SetMotorPID(Location.HIP_LEFT, p, i, d, integralMin, integralMax, outputMin, outputMax);

		if (_motorList[Location.HIP_RIGHT] != null)
			SetMotorPID(Location.HIP_RIGHT, p, i, d, integralMin, integralMax, outputMin, outputMax);
	}

	public void SetLegJointPID(
		float p, float i, float d,
		float integralMin, float integralMax,
		float outputMin, float outputMax)
	{
		if (_motorList[Location.LEG_LEFT] != null)
			SetMotorPID(Location.LEG_LEFT, p, i, d, integralMin, integralMax, outputMin, outputMax);

		if (_motorList[Location.LEG_RIGHT] != null)
			SetMotorPID(Location.LEG_RIGHT, p, i, d, integralMin, integralMax, outputMin, outputMax);
	}

	public void SetHeadJointPID(
		float p, float i, float d,
		float integralMin, float integralMax,
		float outputMin, float outputMax)
	{
		if (_motorList[Location.HEAD] != null)
			SetMotorPID(Location.HEAD, p, i, d, integralMin, integralMax, outputMin, outputMax);
	}

	public void SetHeadJoint(in string targetHeadJointName)
	{
		AttachMotor(Location.HEAD, targetHeadJointName);
		_motorList[Location.HEAD].DriveType = ArticulationDriveType.Acceleration;
	}

	public void SetHipJoints(in string hipJointLeft, in string hipJointright)
	{
		AttachMotor(Location.HIP_LEFT, hipJointLeft);
		AttachMotor(Location.HIP_RIGHT, hipJointright);
		_motorList[Location.HIP_LEFT].DriveType = ArticulationDriveType.Acceleration;
		_motorList[Location.HIP_RIGHT].DriveType = ArticulationDriveType.Acceleration;
	}

	public void SetLegJoints(in string legJointLeft, in string legJointright)
	{
		AttachMotor(Location.LEG_LEFT, legJointLeft);
		AttachMotor(Location.LEG_RIGHT, legJointright);
		_motorList[Location.LEG_LEFT].DriveType = ArticulationDriveType.Acceleration;
		_motorList[Location.LEG_RIGHT].DriveType = ArticulationDriveType.Acceleration;
	}

	public void ChangeWheelDriveType()
	{
		_motorList[Location.FRONT_WHEEL_LEFT].DriveType = ArticulationDriveType.Acceleration;
		_motorList[Location.FRONT_WHEEL_RIGHT].DriveType = ArticulationDriveType.Acceleration;
	}

	private Vector2d GetHipJointPositions()
	{
		if (_motorList[Location.HIP_LEFT] == null || _motorList[Location.HIP_RIGHT] == null)
		{
			return Vector2d.zero;
		}

		return new Vector2d(
			_motorList[Location.HIP_LEFT].GetJointPosition(),
			_motorList[Location.HIP_RIGHT].GetJointPosition()
		);
	}

	private Vector2d GetHipJointVelocities()
	{
		if (_motorList[Location.HIP_LEFT] == null || _motorList[Location.HIP_RIGHT] == null)
		{
			return Vector2d.zero;
		}

		return new Vector2d(
			GetAngularVelocity(Location.HIP_LEFT),
			GetAngularVelocity(Location.HIP_RIGHT)
		);
	}

	private Vector2d GetLegJointPositions()
	{
		if (_motorList[Location.LEG_LEFT] == null || _motorList[Location.LEG_RIGHT] == null)
		{
			return Vector2d.zero;
		}

		return new Vector2d(
			_motorList[Location.LEG_LEFT].GetJointPosition(),
			_motorList[Location.LEG_RIGHT].GetJointPosition()
		);
	}

	private Vector2d GetLegJointVelocities()
	{
		if (_motorList[Location.LEG_LEFT] == null || _motorList[Location.LEG_RIGHT] == null)
		{
			return Vector2d.zero;
		}

		return new Vector2d(
			GetAngularVelocity(Location.LEG_LEFT),
			GetAngularVelocity(Location.LEG_RIGHT)
		);
	}

	private double GetHeadJointPosition()
	{
		return _motorList[Location.HEAD].GetJointPosition();
	}

	private double GetHeadJointVelocity()
	{
		return GetAngularVelocity(Location.HEAD);
	}

	private Vector2d UpdateHip(in Vector2d actual, in Vector2d target, in float duration)
	{
		return new Vector2d(
				_motorList[Location.HIP_LEFT].UpdatePID(actual[0], target[0], duration),
				_motorList[Location.HIP_RIGHT].UpdatePID(actual[1], target[1], duration)
			);
	}

	private double UpdateHead(in double actual, in double target, in float duration)
	{
		return _motorList[Location.HEAD].UpdatePID(actual, target, duration);
	}

	public override void Drive(in float linearVelocity, in float angularVelocity)
	{
		_commandTwistLinear = linearVelocity;
		_commandTwistAngular = -angularVelocity;

		if (_commandTwistLinear == 0)
		{
			PitchTarget = 0;
		}
	}

	private Vector3 GetOrientation(SensorDevices.IMU imuSensor)
	{
		var angles = imuSensor.GetOrientation();
		angles *= Mathf.Deg2Rad; // deg to rad
		angles.NormalizeAngle();
		// Debug.Log("Orientation: " + angles.x * Mathf.Rad2Deg + "," + angles.y * Mathf.Rad2Deg + "," + angles.z * Mathf.Rad2Deg);
		return angles;
	}

	private void SaturateEfforts(ref VectorXd efforts)
	{
		efforts[0] = Mathf.Clamp((float)efforts[0], -WHEEL_MAX_TORQUE, WHEEL_MAX_TORQUE);
		efforts[1] = Mathf.Clamp((float)efforts[1], -WHEEL_MAX_TORQUE, WHEEL_MAX_TORQUE);
		efforts[2] = Mathf.Clamp((float)efforts[2], -HIP_MAX_TORQUE, HIP_MAX_TORQUE);
		efforts[3] = Mathf.Clamp((float)efforts[3], -HIP_MAX_TORQUE, HIP_MAX_TORQUE);
	}

	private void SetEfforts(VectorXd efforts)
	{
		// SaturateEfforts(ref efforts);
		Debug.Log($"Effort:  {efforts}");
		_motorList[Location.FRONT_WHEEL_LEFT]?.SetJointForce((float)efforts[0]);
		_motorList[Location.FRONT_WHEEL_RIGHT]?.SetJointForce((float)efforts[1]);
		_motorList[Location.HIP_LEFT]?.SetJointForce((float)efforts[2]);
		_motorList[Location.HIP_RIGHT]?.SetJointForce((float)efforts[3]);
		_motorList[Location.HEAD]?.SetJointForce((float)efforts[4]);
	}

	public float _detectFalldownThreshold = 1.48f;

	public override bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		// Debug.Log("Update Balanced");
		if (imuSensor == null)
		{
			Debug.LogWarning("IMU sensor is missing");
			return false;
		}

		var wheelVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var wheelVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

		var orientation = GetOrientation(imuSensor);
		var roll = orientation.z;
		var pitch = orientation.x;
		var yaw = orientation.y;

		// Debug.Log(orientation.ToString("F10"));
		// Debug.Log($"wheelVelocity1 L/R: {wheelVelocityLeft}/{wheelVelocityRight}");
		// Debug.Log($"wheelVelocity2 L/R: {wheelVelocityLeft2}/{wheelVelocityRight2}");
		// Debug.Log($"Pitch: {pitch}, Roll: {roll}, Yaw: {yaw}");

		if (_onBalancing && Mathf.Abs(pitch) > _detectFalldownThreshold)
		{
			Debug.LogWarning($"Falldown detected!!!! pitch: {pitch}");
			_onBalancing = false;
			Reset();
		}

		if (_onBalancing == false)
		{
			return true;
		}

		// > observer's update
		var wipStates = _kinematics.ComputeStates(wheelVelocityLeft, wheelVelocityRight, yaw, pitch, roll, duration);

		var commandPitch = _pitchProfiler.Generate(Time.timeAsDouble);
		var commandPitchDerivative = commandPitch / duration;

		// Debug.Log($"Pitch: {commandPitch * Mathf.Rad2Deg},{commandPitchDerivative * Mathf.Rad2Deg} | wheelVelocity L/R: {wheelVelocityLeft.ToString("F6")}/{wheelVelocityRight.ToString("F6")}");

		var wipReferences = _kinematics.GetReferences(
			_commandTwistLinear, _commandTwistAngular,
			commandPitch, commandPitchDerivative,
			duration);

		var wipEfforts = _smc.ComputeControl(wipStates, wipReferences, duration);

		var hipPositions = GetHipJointPositions();
		// var hipVelocities = GetHipJointVelocities();
		var headsetPosition = GetHeadJointPosition();
		// var headsetVelocity = GetHeadJointVelocity();

		var hipEfforts = UpdateHip(hipPositions, _commandHipTarget, duration);
		var headsetEffort = UpdateHead(headsetPosition, _commandHeadsetTarget, duration);
		// Debug.Log($"{headsetPosition} {_commandHeadsetTarget} {headsetEffort} {_motorList[Location.HEAD]?.GetForce()}");

		var effortsZOH = new VectorXd(new double[] {
				Unity2SDF.Direction.Curve(wipEfforts.x),
				Unity2SDF.Direction.Curve(wipEfforts.y),
				hipEfforts.x,
				hipEfforts.y,
				headsetEffort
			});

		// Debug.Log($"Pitch:	 {pitch}, X {effortsZOH[0]}, Y: {effortsZOH[1]}");
		// Debug.Log("Balanced Control: " + wipEfforts + ", " + hipEfforts + ", " + headsetEffort);

		// Torque (ZOH manner)
		SetEfforts(effortsZOH);

		if ((odomMessage != null) &&
			!float.IsNaN(wheelVelocityLeft) && !float.IsNaN(wheelVelocityRight))
		{
			odomMessage.AngularVelocity.Left = Unity2SDF.Direction.Curve(wheelVelocityLeft);
			odomMessage.AngularVelocity.Right = Unity2SDF.Direction.Curve(wheelVelocityRight);
			odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * _kinematics.WheelInfo.wheelRadius;
			odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * _kinematics.WheelInfo.wheelRadius;

			var odom = _kinematics.GetOdom();
			var rotation = _kinematics.GetRotation();
			var odomPose = new Vector3((float)odom.y, (float)rotation.z, (float)odom.x);
			odomMessage.Pose.Set(Unity2SDF.Direction.Reverse(odomPose));
			return true;
		}

		return false;
	}
}