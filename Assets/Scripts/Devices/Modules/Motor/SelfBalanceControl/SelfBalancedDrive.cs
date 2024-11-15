/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using SelfBalanceControl;
using UnityEngine;
using System;
using messages = cloisim.msgs;

public class SelfBalancedDrive : MotorControl
{
	private Kinematics _kinematics = null;
	private ExpProfiler _pitchProfiler;
	private SlidingModeControl _smc = null;

	private bool _onBalancing = false;
	private bool _resetPose = false;

	private double _commandTwistLinear = 0;
	private double _commandTwistAngular = 0;

	#region Body Control
	private double _commandTargetBody = 0; // in deg
	#endregion

	#region Roll/Height Control
	private double _commandTargetHeight = 0; // in deg
	private double _commandTargetRoll = 0; // in deg
	private double _commandTargetRollByDrive = 0; // in deg
	private float _doControlRollHeightByCommandTimeout = 0;
	#endregion

	#region Pitch Control
	private double _commandTargetPitch = 0; // in rad
	private double _prevCommandPitch = 0;
	private bool _doUpdatePitchProfiler = false;
	private float _doControlPitchByCommandTimeout = 0;
	#endregion

	#region Headset Control
	private double _commandTargetHeadset = 0; // in deg
	private float _doControlHeadsetByCommandTimeout = 0;
	#endregion

	private Vector2d _commandHipTarget = Vector2d.zero; // in deg
	private Vector2d _commandLegTarget = Vector2d.zero; // in deg

	#region Limitation
	private readonly float CommandTimeout = 5f;
	private readonly double CommandMargin = 0.00001;
	private readonly MathUtil.MinMax FalldownPitchThreshold = new MathUtil.MinMax(-1.25f, 1.35f); // in rad
	private readonly MathUtil.MinMax FalldownRollThreshold = new MathUtil.MinMax(-1.35f, 1.35f); // in rad
	private readonly MathUtil.MinMax RollLimit = new MathUtil.MinMax(-5f, 5f); // in deg
	private readonly MathUtil.MinMax HeightLimit = new MathUtil.MinMax(-30f, 22f); // in deg
	private readonly MathUtil.MinMax HeadsetLimit = new MathUtil.MinMax(-90f, 90f); // in deg
	#endregion

	public double HeightTarget
	{
		get => _commandTargetHeight;
		set
		{
			_commandTargetHeight = Math.Clamp(value, HeightLimit.min + CommandMargin, HeightLimit.max - CommandMargin);
			_doControlRollHeightByCommandTimeout = CommandTimeout;
		}
	}

	public double RollTarget
	{
		get => _commandTargetRoll;
		set
		{
			_commandTargetRoll = Math.Clamp(value, RollLimit.min + CommandMargin, RollLimit.max - CommandMargin);
			_doControlRollHeightByCommandTimeout = CommandTimeout;
		}
	}

	public double PitchTarget
	{
		get => _commandTargetPitch;
		set
		{
			_commandTargetPitch = Math.Clamp(value, FalldownPitchThreshold.min + CommandMargin, FalldownPitchThreshold.max - CommandMargin);
			_doUpdatePitchProfiler = true;
			_doControlPitchByCommandTimeout = CommandTimeout;
		}
	}

	public double HeadsetTarget
	{
		get => _commandTargetHeadset;
		set
		{
			_commandTargetHeadset = Math.Clamp(value, HeadsetLimit.min + CommandMargin, HeadsetLimit.max - CommandMargin);
			_doControlHeadsetByCommandTimeout = CommandTimeout;
		}
	}

	public double HeightTargetMin => HeadsetLimit.min;
	public double HeightTargetMax => HeadsetLimit.max;

	public bool Balancing
	{
		get => _onBalancing;
		set => _onBalancing = value;
	}

	public void DoResetPose()
	{
		_resetPose = true;
	}

	public SelfBalancedDrive(
		in Transform controllerTransform,
		in SlidingModeControl.OutputMode outputMode,
		in SlidingModeControl.SwitchingMode switchingMode,
		in double expProfilerTimeConstant)
		: base(controllerTransform)
	{
		_pitchProfiler = new ExpProfiler(expProfilerTimeConstant);
		_smc = new SlidingModeControl(outputMode, switchingMode);
	}

	public SelfBalancedDrive(
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
		Debug.Log("========== SelfBalancedDrive Reset ==========");

		_commandTargetHeadset = 0;
		_commandTargetRollByDrive = 0;

		_commandTargetPitch = 0;
		_doUpdatePitchProfiler = false;
		_prevCommandPitch = 0;
		UpdatePitchProfiler();

		_commandHipTarget = Vector2d.zero;
		_commandLegTarget = Vector2d.zero;

		foreach (var wheel in _motorList)
		{
			wheel.Value?.Reset();
		}

		var wheelVelocityLeft = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
		var wheelVelocityRight = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

		ResetPose();
		_resetPose = false;

		_kinematics.Reset(wheelVelocityLeft, wheelVelocityRight);
		_smc.Reset();

		ResetJoints();
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

	public void SetHeadJoint(in string targetJointName)
	{
		AttachMotor(Location.HEAD, targetJointName);
		ChangeDriveType(Location.HEAD, ArticulationDriveType.Target);
	}

	public void SetBodyJoint(in string targetJointName)
	{
		AttachMotor(Location.BODY, targetJointName);
		ChangeDriveType(Location.BODY, ArticulationDriveType.Target);
	}

	public void SetHipJoints(in string hipJointLeft, in string hipJointright)
	{
		AttachMotor(Location.HIP_LEFT, hipJointLeft);
		AttachMotor(Location.HIP_RIGHT, hipJointright);
		ChangeDriveType(Location.HIP_LEFT, ArticulationDriveType.Target);
		ChangeDriveType(Location.HIP_RIGHT, ArticulationDriveType.Target);
	}

	public void SetLegJoints(in string legJointLeft, in string legJointright)
	{
		AttachMotor(Location.LEG_LEFT, legJointLeft);
		AttachMotor(Location.LEG_RIGHT, legJointright);
		ChangeDriveType(Location.LEG_LEFT, ArticulationDriveType.Target);
		ChangeDriveType(Location.LEG_RIGHT, ArticulationDriveType.Target);
	}

	public void ChangeWheelDriveType()
	{
		ChangeDriveType(Location.FRONT_WHEEL_LEFT, ArticulationDriveType.Acceleration);
		ChangeDriveType(Location.FRONT_WHEEL_RIGHT, ArticulationDriveType.Acceleration);
	}

	public override void Drive(in float linearVelocity, in float angularVelocity)
	{
		_commandTwistLinear = linearVelocity;
		_commandTwistAngular = SDF2Unity.CurveOrientationAngle(angularVelocity);

		if (Math.Abs(_commandTwistLinear) < float.Epsilon || Math.Abs(_commandTwistAngular) < float.Epsilon)
		{
			PitchTarget = 0;
			_commandTargetRollByDrive = 0;
		}

		if (Math.Abs(_commandTwistLinear) > float.Epsilon && Math.Abs(_commandTwistAngular) > float.Epsilon)
		{
			var ratio = _commandTwistAngular / _commandTwistLinear;
			_commandTargetRollByDrive = ((ratio > 0) ? RollLimit.min : RollLimit.max) * -Math.Abs(ratio);
			// Debug.Log($"Command - linear: {_commandTwistLinear} angular: {_commandTwistAngular} ratio: {ratio} _commandTargetRollByDrive: {_commandTargetRollByDrive}");
		}
		// Debug.Log($"Command - linear: {_commandTwistLinear} angular: {_commandTwistAngular} pitch: {PitchTarget}");
	}

	private void UpdatePitchProfiler()
	{
		_pitchProfiler.Reset(Time.timeAsDouble, _commandTargetPitch);
	}

	private void ResetJoints()
	{
		SetWheelEfforts(VectorXd.Zero(2));
		SetJoints(VectorXd.Zero(6));
	}

	private void ResetPose()
	{
		_commandTargetHeight = 0;
		_commandTargetRoll = 0;

		_commandHipTarget = Vector2d.zero;

		_motorList[Location.HIP_LEFT]?.Reset();
		_motorList[Location.HIP_RIGHT]?.Reset();

		if (_motorList.ContainsKey(Location.LEG_LEFT) &&
			_motorList.ContainsKey(Location.LEG_RIGHT))
		{
			_motorList[Location.LEG_LEFT]?.Reset();
			_motorList[Location.LEG_RIGHT]?.Reset();
		}

		ResetJoints();
	}

	private void RestorePitchZero(in double currentCommandPitch, in float duration)
	{
		if (Math.Abs(currentCommandPitch) < Quaternion.kEpsilon)
		{
			_commandTargetPitch = Mathf.Lerp((float)currentCommandPitch, 0, duration);
			// Debug.LogWarning("comandTargetPitch reset!!!");
		}
	}

	private void AdjustHeadsetByPitch(in double currentPitch, in float duration)
	{
		const double HeadsetTargetAdjustGain = 1.5f;
		var target = currentPitch * HeadsetTargetAdjustGain * Mathf.Rad2Deg;
		_commandTargetHeadset = Mathf.Lerp((float)_commandTargetHeadset, (float)target, duration);
		// Debug.LogWarning("Adjusting head by pitch");
	}

	private void ControlHipAndLeg(in double currentRoll)
	{
		const float BodyUpperGain = 0.9f;
		const float BodyLowerGain = 1.4f;

		_commandTargetBody = _commandTargetHeight * ((_commandTargetHeight >= 0) ? BodyUpperGain : BodyLowerGain);
		// Debug.Log($"_commandTargetHeight: {_commandTargetHeight} _commandTargetBody: {_commandTargetBody}");

		_commandHipTarget.x = _commandTargetHeight;
		_commandHipTarget.y = _commandTargetHeight;

		_commandLegTarget.x = _commandTargetHeight;
		_commandLegTarget.y = _commandTargetHeight;

		_commandHipTarget.x += _commandTargetRoll;
		_commandHipTarget.y += -_commandTargetRoll;

		_commandLegTarget.x += -_commandTargetRoll;
		_commandLegTarget.y += _commandTargetRoll;
	}

	private float _smoothControlTime = 0;
	private double _prevCommandTargetRollByDrive = 0;

	private void ControlSmoothRollTarget(in float duration)
	{
		const float smoothTimeDiff = 0.00005f;
		// Debug.Log($"_commandTwistLinear: {_commandTwistLinear} _kinematics.OdomTranslationalVelocity: {_kinematics.OdomTranslationalVelocity}");
		if ((_commandTwistLinear > 0 && _kinematics.OdomTranslationalVelocity > 0) ||
			(_commandTwistLinear < 0 && _kinematics.OdomTranslationalVelocity < 0) )
		{
			if (Math.Abs(_commandTargetRollByDrive - _prevCommandTargetRollByDrive) > Quaternion.kEpsilon)
			{
				_smoothControlTime = 0;
				// Debug.LogWarning("Reset _smoothControlTime");
			}

			_commandTargetRoll = Mathf.Lerp((float)_commandTargetRoll, (float)_commandTargetRollByDrive, _smoothControlTime += smoothTimeDiff);
			// Debug.Log($"_commandTargetRoll: {_commandTargetRoll} _commandTargetRollByDrive: {_commandTargetRollByDrive} _smoothControlTime: {_smoothControlTime}");
			_prevCommandTargetRollByDrive = _commandTargetRollByDrive;
		}
	}

	private void RestoreHipAndLegZero(in float duration)
	{
		_commandTargetRoll = Mathf.Lerp((float)_commandTargetRoll, 0, duration);
		_commandTargetHeight = Mathf.Lerp((float)_commandTargetHeight, 0, duration);
	}

	private Vector3 GetOrientation(SensorDevices.IMU imuSensor)
	{
		var angles = imuSensor.GetOrientation();
		angles *= Mathf.Deg2Rad;
		angles.NormalizeAngle();
		// Debug.Log("Orientation: " + angles.x * Mathf.Rad2Deg + "," + angles.y * Mathf.Rad2Deg + "," + angles.z * Mathf.Rad2Deg);
		return angles;
	}

	private void SetWheelEfforts(in Vector2d efforts)
	{
		// Debug.Log($"Effort:  {efforts}");
		_motorList[Location.FRONT_WHEEL_LEFT]?.SetJointForce((float)Unity2SDF.Direction.Curve(efforts.x));
		_motorList[Location.FRONT_WHEEL_RIGHT]?.SetJointForce((float)Unity2SDF.Direction.Curve(efforts.y));
	}

	private void SetJoints(in VectorXd targets)
	{
		if (_motorList.ContainsKey(Location.HEAD))
		{
			_motorList[Location.HEAD]?.Drive(targetPosition: (float)targets[0]);
		}

		if (_motorList.ContainsKey(Location.BODY))
		{
			_motorList[Location.BODY]?.Drive(targetPosition: (float)targets[1]);
		}

		_motorList[Location.HIP_LEFT]?.Drive(targetPosition: (float)targets[2]);
		_motorList[Location.HIP_RIGHT]?.Drive(targetPosition: (float)targets[3]);

		if (_motorList.ContainsKey(Location.LEG_LEFT) &&
			_motorList.ContainsKey(Location.LEG_RIGHT))
		{
			_motorList[Location.LEG_LEFT]?.Drive(targetPosition: (float)targets[4]);
			_motorList[Location.LEG_RIGHT]?.Drive(targetPosition: (float)targets[5]);
		}
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
		var roll = imuRotation.z;

		if (_onBalancing)
		{
			if (pitch > FalldownPitchThreshold.max || pitch < FalldownPitchThreshold.min ||
				roll > FalldownRollThreshold.max || roll < FalldownRollThreshold.min)
			{
				Debug.LogWarning($"Falldown detected !!! pitch: {pitch * Mathf.Rad2Deg} roll: {roll * Mathf.Rad2Deg}");
				_onBalancing = false;
				Reset();
			}
			else
			{
				var wheelVelocity = Vector2.zero;
				wheelVelocity.x = GetAngularVelocity(Location.FRONT_WHEEL_LEFT);
				wheelVelocity.y = GetAngularVelocity(Location.FRONT_WHEEL_RIGHT);

				ProcessBalancing(wheelVelocity, duration, imuRotation);

				if ((odomMessage != null) &&
					!float.IsNaN(wheelVelocity.x) && !float.IsNaN(wheelVelocity.y))
				{
					odomMessage.AngularVelocity.Left = Unity2SDF.Direction.Curve(wheelVelocity.x);
					odomMessage.AngularVelocity.Right = Unity2SDF.Direction.Curve(wheelVelocity.y);
					odomMessage.LinearVelocity.Left = odomMessage.AngularVelocity.Left * _kinematics.WheelInfo.wheelRadius;
					odomMessage.LinearVelocity.Right = odomMessage.AngularVelocity.Right * _kinematics.WheelInfo.wheelRadius;

					var odom = _kinematics.GetOdom();
					var rotation = _kinematics.GetRotation();
					var odomPose = new Vector3((float)odom.y, (float)rotation.z, (float)odom.x);
					odomMessage.Pose.Set(Unity2SDF.Direction.Reverse(odomPose));

					odomMessage.TwistLinear.X = _kinematics.OdomTranslationalVelocity;
					odomMessage.TwistAngular.Z = _kinematics.OdomRotationalVelocity;
				}
				else
				{
					Debug.LogWarning($"Odometry is missing or Problem with wheelVelocity {wheelVelocity.x}|{wheelVelocity.y}");
					return false;
				}
			}
		}

		return true;
	}

	private void ProcessBalancing(in Vector2 wheelVelocity, in float duration, Vector3 imuRotation)
	{
		var wheelVelocityLeft = wheelVelocity.x;
		var wheelVelocityRight = wheelVelocity.y;
		var roll = imuRotation.z;
		var pitch = imuRotation.x;
		var yaw = imuRotation.y;

		if (_resetPose)
		{
			ResetPose();
			_resetPose= false;
		}

		var wipStates = _kinematics.ComputeStates(wheelVelocityLeft, wheelVelocityRight, yaw, pitch, roll, duration);

		if (_doUpdatePitchProfiler)
		{
			UpdatePitchProfiler();
		}

		var wipReferences = GetTargetReferences(duration);
		// Debug.Log($"Cmd lin: {_commandTwistLinear} ang: {_commandTwistAngular} " +
		// 		$"pitch: {wipReferences[3].ToString("F5")}=={(wipReferences[3] * Mathf.Rad2Deg).ToString("F5")} " +
		// 		$"pitchd: {(wipReferences[2] * Mathf.Rad2Deg).ToString("F4")} | "+
		// 		$"Current pitch: {pitch.ToString("F4")}=={(pitch * Mathf.Rad2Deg).ToString("F4")} " +
		// 		$"pitchDot: {wipStates[2].ToString("F4")}=={(wipStates[2] * Mathf.Rad2Deg).ToString("F4")} | "+
		// 		$"wheelVel L/R: {wheelVelocityLeft.ToString("F5")}/{wheelVelocityRight.ToString("F5")}");

		if (!_doUpdatePitchProfiler)
		{
			RestorePitchZero(wipReferences[2], duration);
		}
		else
		{
			if ((_doControlPitchByCommandTimeout -= duration) < float.Epsilon)
			{
				_doUpdatePitchProfiler = false;
			}
		}

		if (Math.Abs(_commandTargetRollByDrive) > float.Epsilon)
		{
			ControlSmoothRollTarget(duration);
		}
		else if ((_doControlRollHeightByCommandTimeout -= duration) < float.Epsilon)
		{
			RestoreHipAndLegZero(duration);
		}

		ControlHipAndLeg(roll);

		if ((_doControlHeadsetByCommandTimeout -= duration) < float.Epsilon)
		{
			AdjustHeadsetByPitch(wipStates[3], duration);
		}

		var wipEfforts = _smc.ComputeControl(wipStates, wipReferences, duration);

		SetWheelEfforts(wipEfforts);

		var jointTargets = new VectorXd(new double[] {
				_commandTargetHeadset,
				_commandTargetBody,
				_commandHipTarget.x, _commandHipTarget.y,
				_commandLegTarget.x, _commandLegTarget.y,
			});
		SetJoints(jointTargets);
	}
}