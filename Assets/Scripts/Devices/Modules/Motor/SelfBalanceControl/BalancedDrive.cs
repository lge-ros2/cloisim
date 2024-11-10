/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using SelfBalanceControl;
using UnityEngine;
using System;
using messages = cloisim.msgs;

public class BalancedDrive : MotorControl
{
	private Kinematics _kinematics = null;
	private ExpProfiler _pitchProfiler;
	private SlidingModeControl _smc = null;

	private bool _onBalancing = false;
	private double _commandTwistLinear = 0;
	private double _commandTwistAngular = 0;
	private double _commandTargetPitch = 0;
	private double _prevCommandPitch = 0;
	private bool _doUpdatePitchProfiler = false;
	private double _commandHeadsetTarget = 0;
	private Vector2d _commandHipTarget = Vector2d.zero;
	private Vector2d _commandLegTarget = Vector2d.zero;
	private float _detectFalldownThresholdMin = -1.25f;
	private float _detectFalldownThresholdMax = 1.35f;

	public double PitchTarget
	{
		get => _commandTargetPitch;
		set
		{
			const double commandMargin = 0.00001;
			_commandTargetPitch = Math.Clamp(value, _detectFalldownThresholdMin + commandMargin, _detectFalldownThresholdMax - commandMargin);
			_doUpdatePitchProfiler = true;
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

	public Vector2d LegTarget
	{
		get => _commandLegTarget;
		set => _commandLegTarget = value;
	}

	public bool Balancing
	{
		get => _onBalancing;
		set => _onBalancing = value;
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
		Debug.Log("========== BalancedDrive Reset ==========");

		_commandTargetPitch = 0;
		_doUpdatePitchProfiler = false;
		_prevCommandPitch = 0;
		UpdatePitchProfiler();

		_commandHeadsetTarget = 0;
		_commandHipTarget = Vector2d.zero;
		_commandLegTarget = Vector2d.zero;

		foreach (var wheel in _motorList)
		{
			wheel.Value?.Reset();
		}

		var wheelVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var wheelVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

		ResetPose();

		_kinematics.Reset(wheelVelocityLeft, wheelVelocityRight);
		_smc.Reset();

		SetEfforts(VectorXd.Zero(7));
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

	private Vector2d UpdateLeg(in Vector2d actual, in Vector2d target, in float duration)
	{
		return new Vector2d(
				_motorList[Location.LEG_LEFT].UpdatePID(actual[0], target[0], duration),
				_motorList[Location.LEG_RIGHT].UpdatePID(actual[1], target[1], duration)
			);
	}

	private double UpdateHead(in double actual, in double target, in float duration)
	{
		return _motorList[Location.HEAD].UpdatePID(actual, target, duration);
	}

	public override void Drive(in float linearVelocity, in float angularVelocity)
	{
		_commandTwistLinear = linearVelocity;
		_commandTwistAngular = SDF2Unity.CurveOrientationAngle(angularVelocity);

		if (_commandTwistLinear == 0)
		{
			PitchTarget = 0;
		}
		// Debug.Log($"Command - linear: {_commandTwistLinear} angular: {_commandTwistAngular} pitch: {PitchTarget}");
	}

	private void UpdatePitchProfiler()
	{
		_pitchProfiler.Reset(Time.timeAsDouble, _commandTargetPitch);
	}

	private void ResetPose()
	{
		_motorList[Location.HIP_LEFT]?.Reset();
		_motorList[Location.HIP_RIGHT]?.Reset();

		if (_motorList.ContainsKey(Location.LEG_LEFT) &&
			_motorList.ContainsKey(Location.LEG_RIGHT))
		{
			_motorList[Location.LEG_LEFT]?.Reset();
			_motorList[Location.LEG_RIGHT]?.Reset();
		}

		SetEfforts(VectorXd.Zero(7));
	}

	private void ResetCommandPitch(in double currentCommandPitch)
	{
		if (Math.Abs(currentCommandPitch) < Quaternion.kEpsilon)
		{
			_commandTargetPitch = 0;
			// Debug.LogWarning("comandTargetPitch reset!!!");
		}
	}

	private void AdjustHeadsetByPitch(in double currentPitch)
	{
		_commandHeadsetTarget = currentPitch;
	}

	private Vector3 GetOrientation(SensorDevices.IMU imuSensor)
	{
		var angles = imuSensor.GetOrientation();
		angles *= Mathf.Deg2Rad; // deg to rad
		angles.NormalizeAngle();
		// Debug.Log("Orientation: " + angles.x * Mathf.Rad2Deg + "," + angles.y * Mathf.Rad2Deg + "," + angles.z * Mathf.Rad2Deg);
		return angles;
	}

	private void SetEfforts(VectorXd efforts)
	{
		// Debug.Log($"Effort:  {efforts}");
		_motorList[Location.FRONT_WHEEL_LEFT]?.SetJointForce((float)efforts[0]);
		_motorList[Location.FRONT_WHEEL_RIGHT]?.SetJointForce((float)efforts[1]);
		_motorList[Location.HIP_LEFT]?.SetJointForce((float)efforts[2]);
		_motorList[Location.HIP_RIGHT]?.SetJointForce((float)efforts[3]);

		if (_motorList.ContainsKey(Location.LEG_LEFT) &&
			_motorList.ContainsKey(Location.LEG_RIGHT))
		{
			_motorList[Location.LEG_LEFT]?.SetJointForce((float)efforts[4]);
			_motorList[Location.LEG_RIGHT]?.SetJointForce((float)efforts[5]);
		}

		_motorList[Location.HEAD]?.SetJointForce((float)efforts[6]);
	}

	private VectorXd GetTargetReferences(in float duration)
	{
		var commandPitch = _pitchProfiler.Generate(Time.timeAsDouble);
		var commandPitchDerivative = (commandPitch - _prevCommandPitch) / Time.timeAsDouble;
		// Debug.Log($"new commandPitch: {commandPitch}  _commandTargetPitch: {_commandTargetPitch} commandPitchDerivative: {commandPitchDerivative}");
		_prevCommandPitch = commandPitch;

		return _kinematics.GetReferences(
			_commandTwistLinear, _commandTwistAngular,
			commandPitch, commandPitchDerivative,
			duration);
	}

	public override bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor)
	{
		// Debug.Log("Update Balanced");
		if (imuSensor == null)
		{
			Debug.LogWarning("IMU sensor is missing");
			return false;
		}

		var imuRotation = GetOrientation(imuSensor);
		var pitch = imuRotation.x;

		if (_onBalancing && (pitch > _detectFalldownThresholdMax || pitch < _detectFalldownThresholdMin))
		{
			Debug.LogWarning($"Falldown detected !!! pitch: {pitch * Mathf.Rad2Deg}");
			_onBalancing = false;
			Reset();
		}

		if (_onBalancing)
		{
			return ProcessBalancing(odomMessage, duration, imuRotation);
		}

		return true;
	}

	private bool ProcessBalancing(messages.Micom.Odometry odomMessage, in float duration, Vector3 imuRotation)
	{
		var wheelVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var wheelVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

		var roll = imuRotation.z;
		var pitch = imuRotation.x;
		var yaw = imuRotation.y;

		var wipStates = _kinematics.ComputeStates(wheelVelocityLeft, wheelVelocityRight, yaw, pitch, roll, duration);
		// Debug.Log($"wipStates: {wipStates}");

		var pitchUpdated = false;
		if (_doUpdatePitchProfiler)
		{
			UpdatePitchProfiler();
			pitchUpdated = true;
			_doUpdatePitchProfiler = false;
		}

		var wipReferences = GetTargetReferences(duration);
		// Debug.Log($"Cmd lin: {_commandTwistLinear} ang: {_commandTwistAngular} " +
		// 		$"pitch: {wipReferences[3].ToString("F5")}=={(wipReferences[3] * Mathf.Rad2Deg).ToString("F5")} " +
		// 		$"pitchd: {(wipReferences[2] * Mathf.Rad2Deg).ToString("F4")} | "+
		// 		$"Current pitch: {pitch.ToString("F4")}=={(pitch * Mathf.Rad2Deg).ToString("F4")} " +
		// 		$"pitchDot: {wipStates[2].ToString("F4")}=={(wipStates[2] * Mathf.Rad2Deg).ToString("F4")} | "+
		// 		$"wheelVel L/R: {wheelVelocityLeft.ToString("F5")}/{wheelVelocityRight.ToString("F5")}");

		if (!pitchUpdated)
		{
			ResetCommandPitch(wipReferences[2]);
		}

		AdjustHeadsetByPitch(wipStates[3]);

		var wipEfforts = _smc.ComputeControl(wipStates, wipReferences, duration);

		var hipPositions = GetHipJointPositions();
		// var hipVelocities = GetHipJointVelocities();
		var headsetPosition = GetHeadJointPosition();
		// var headsetVelocity = GetHeadJointVelocity();
		var legPositions = GetLegJointPositions();
		// var legVelocities = GetLegJointVelocities();

		var headsetEffort = UpdateHead(headsetPosition, _commandHeadsetTarget, duration);
		var hipEfforts = UpdateHip(hipPositions, _commandHipTarget, duration);
		var legEfforts = UpdateLeg(legPositions, _commandLegTarget, duration);
		// Debug.Log($"{headsetPosition} {_commandHeadsetTarget} {headsetEffort} {_motorList[Location.HEAD]?.GetForce()}");

		// Torque (ZOH manner)
		var efforts = new VectorXd(new double[] {
				Unity2SDF.Direction.Curve(wipEfforts.x),
				Unity2SDF.Direction.Curve(wipEfforts.y),
				hipEfforts.x,
				hipEfforts.y,
				legEfforts.x,
				legEfforts.y,
				headsetEffort
			});
		SetEfforts(efforts);

		// Debug.Log($"Pitch:	 {pitch}, X {efforts[0]}, Y: {efforts[1]}");
		// Debug.Log("Balanced Control: " + wipEfforts + ", " + hipEfforts + ", " + headsetEffort);

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