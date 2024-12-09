/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class Articulation
{
	protected const int DOF = 6;
	protected ArticulationBody _jointBody = null;
	protected ArticulationJointType _jointType = ArticulationJointType.FixedJoint;
	private ArticulationDriveType _driveType = ArticulationDriveType.Force;

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
	protected float _velocityLimit = float.NaN;
#endif

	public ArticulationDriveType DriveType
	{
		get => _driveType;
		set {
			_driveType = value;

			var xDrive = _jointBody.xDrive;
			var yDrive = _jointBody.yDrive;
			var zDrive = _jointBody.zDrive;

			xDrive.driveType = _driveType;
			yDrive.driveType = _driveType;
			zDrive.driveType = _driveType;

			_jointBody.xDrive = xDrive;
			_jointBody.yDrive = yDrive;
			_jointBody.zDrive = zDrive;
		}
	}

	public Articulation(in ArticulationBody jointBody)
	{
		if (jointBody != null)
		{
			_jointBody = jointBody;
			_jointType = jointBody.jointType;
		}

		Reset();
	}

	public Articulation(in GameObject target)
		: this(target.GetComponentInChildren<ArticulationBody>())
	{
	}

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
	public void SetVelocityLimit(in float value)
	{
		_velocityLimit = value;
	}
#endif

	public virtual void Reset()
	{
		if (_jointBody != null)
		{
			_jointBody.velocity = Vector3.zero;
			_jointBody.angularVelocity = Vector3.zero;

			SetJointVelocity(0);
		}
	}

	public bool IsRevoluteType()
	{
		return (
			_jointType == ArticulationJointType.RevoluteJoint ||
			_jointType == ArticulationJointType.SphericalJoint) ? true : false;
	}

	public bool IsPrismaticType()
	{
		return (
			_jointType == ArticulationJointType.RevoluteJoint ||
			_jointType == ArticulationJointType.PrismaticJoint) ? true : false;
	}

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
	private float GetLimitedVelocity(in float velocity)
	{
		return (!float.IsNaN(_velocityLimit) && Mathf.Abs(velocity) > Mathf.Abs(_velocityLimit)) ?
				Mathf.Sign(velocity) * Mathf.Abs(_velocityLimit) : velocity;
	}
#endif

	protected void SetJointVelocity(in float velocity, in int targetDegree = 0)
	{
		if (_jointBody != null)
		{
			var jointVelocity = _jointBody.jointVelocity;
			if (targetDegree < jointVelocity.dofCount)
			{
#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
				jointVelocity[targetDegree] = GetLimitedVelocity(velocity);
#else
				jointVelocity[targetDegree] = velocity;
#endif
				_jointBody.jointVelocity = jointVelocity;
			}
		}
	}

	private int GetValidIndex(in int index)
	{
		return (_jointBody == null) ? -1 : ((index >= _jointBody.dofCount || index >= DOF) ? (_jointBody.dofCount - 1) : index);
	}

	public Vector3 GetAnchorRotation()
	{
		return (_jointBody == null) ? Vector3.zero : _jointBody.anchorRotation.eulerAngles;
	}

	/// <returns>in (rad)ian for angular OR in (m)eters for linear</param>
	public float GetJointPosition(int index = 0)
	{
		if (_jointBody == null || index < 0)
		{
			return 0;
		}

		if (IsRevoluteType())
		{
			return _jointBody.jointPosition[index];
		}
		else
		{
			if (_jointType == ArticulationJointType.PrismaticJoint)
			{
				if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
					_jointBody.linearLockY == ArticulationDofLock.LockedMotion)
				{
					return _jointBody.transform.localPosition.z;
				}
				else if (_jointBody.linearLockY == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					return _jointBody.transform.localPosition.x;
				}
				else if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					return _jointBody.transform.localPosition.y;
				}
			}
			else
			{
				Debug.LogWarning("Unsupported articulation JointType: " + _jointType);
			}
		}

		return 0;
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetJointForce(int index = 0)
	{
		index = GetValidIndex(index);
		// Debug.Log(_jointBody.name + ": " + _jointBody.dofCount + ", " + _jointBody.jointAcceleration[0] + ", " + _jointBody.jointForce[0]);
		return (_jointBody == null || index < 0 || _jointBody.IsSleeping()) ? 0 : _jointBody.jointForce[index];
	}

	/// <returns>in radian for angular and in meters for linear</param>
	public float GetJointVelocity(int index = 0)
	{
		index = GetValidIndex(index);
		var value = (_jointBody == null || index < 0 || _jointBody.IsSleeping()) ? 0 : _jointBody.jointVelocity[index];
		return (Mathf.Abs(value) < Quaternion.kEpsilon) ? 0 : value;
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetForce(int index = 0)
	{
		index = GetValidIndex(index);
		var value = (_jointBody == null || index < 0 || _jointBody.IsSleeping()) ? 0 : _jointBody.driveForce[index];
		return value;
	}

	public void SetJointForce(in float force, in int targetDegree = 0)
	{
		if (_jointBody != null)
		{
			var jointForce = _jointBody.jointForce;
			if (targetDegree < jointForce.dofCount)
			{
				jointForce[targetDegree] = force;
				_jointBody.jointForce = jointForce;
			}
		}
	}

	public void SetJointFriction(in float friction)
	{
		if (_jointBody != null)
		{
			_jointBody.jointFriction = friction;
		}
	}

	/// <returns>radian for angular and meter for linear</param>
	public float GetDriveTarget(in int targetDegree = 0)
	{
		if (_jointBody != null)
		{
			var targets = new List<float>();
			_jointBody.GetDriveTargets(targets);

			return targets[targetDegree];
		}
		return 0;
	}

	/// <param name="driveType">ArticulationDriveType</param>
	/// <param name="targetVelocity">angular velocity in degrees per second</param>
	/// <param name="targetPosotion">target position, degree or meter</param>
	public void Drive(
		in float targetVelocity = float.NaN,
		in float targetPosition = float.NaN)
	{
		if (_jointBody == null)
		{
			Debug.LogWarning("ArticulationBody is empty, please set target body first");
			return;
		}

		var drivceAxis = GetDriveAxis();
		// Debug.LogWarning($"targetVelocity={targetVelocity} targetPosition={targetPosition} Type={_driveType}");

		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).

		if (_driveType == ArticulationDriveType.Acceleration ||
			_driveType == ArticulationDriveType.Force ||
			_driveType == ArticulationDriveType.Velocity)
		{
			if (!float.IsNaN(targetVelocity))
			{
				var limitedTargetVelocity = GetLimitedVelocity(targetVelocity);
				_jointBody.SetDriveTargetVelocity(drivceAxis, limitedTargetVelocity);
			}
		}

		if (_driveType == ArticulationDriveType.Acceleration ||
			_driveType == ArticulationDriveType.Force ||
			_driveType == ArticulationDriveType.Target)
		{
			if (!float.IsNaN(targetPosition))
			{
				_jointBody.SetDriveTarget(drivceAxis, targetPosition);
			}
		}
	}

	private ArticulationDriveAxis GetDriveAxis()
	{
		var axis = new ArticulationDriveAxis();

		switch (_jointType)
		{
			case ArticulationJointType.RevoluteJoint:
				axis = ArticulationDriveAxis.X;
				break;

			case ArticulationJointType.PrismaticJoint:

				if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
					_jointBody.linearLockY == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.Z;
				}
				else if (_jointBody.linearLockY == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.X;
				}
				else if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.Y;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + _jointBody.name);
					goto default;
				}
				break;

			case ArticulationJointType.SphericalJoint:

				if (_jointBody.swingYLock == ArticulationDofLock.LockedMotion &&
					_jointBody.swingZLock == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.X;
				}
				else if (_jointBody.swingYLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.Z;
				}
				else if (_jointBody.swingZLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					axis = ArticulationDriveAxis.Y;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + _jointBody.name);
					goto default;
				}
				break;

			default:
				Debug.LogWarning("GetDriveAxis() unsupported joint type: " + _jointType);
				axis = ArticulationDriveAxis.X;
				break;
		}

		return axis;
	}
}