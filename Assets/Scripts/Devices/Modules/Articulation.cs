/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Articulation
{
	protected ArticulationBody _jointBody = null;
	protected ArticulationJointType _jointType = ArticulationJointType.FixedJoint;
	private ArticulationDriveType _driveType = ArticulationDriveType.Force;
	protected float _velocityLimit = float.NaN;

	protected ArticulationDriveType DriveType
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

	public void SetVelocityLimit(in float value)
	{
		_velocityLimit = value;
	}

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

	private float GetLimitedVelocity(in float velocity)
	{
		return (!float.IsNaN(_velocityLimit) && Mathf.Abs(velocity) > Mathf.Abs(_velocityLimit)) ?
				Mathf.Sign(velocity) * Mathf.Abs(_velocityLimit) : velocity;
	}

	protected void SetJointVelocity(in float velocity, in int targetDegree = 0)
	{
		if (_jointBody != null)
		{
			var jointVelocity = _jointBody.jointVelocity;
			if (targetDegree < jointVelocity.dofCount)
			{
				jointVelocity[targetDegree] = GetLimitedVelocity(velocity);
				_jointBody.jointVelocity = jointVelocity;
			}
		}
	}

	private int GetValidIndex(in int index)
	{
		return (_jointBody == null) ? -1 : ((index >= _jointBody.dofCount) ? (_jointBody.dofCount - 1) : index);
	}

	public Vector3 GetAnchorRotation()
	{
		return (_jointBody == null) ? Vector3.zero : _jointBody.anchorRotation.eulerAngles;
	}

	/// <returns>in (rad)ian for angular OR in (m)eters for linear</param>
	public float GetJointPosition(int index = 0)
	{
		if (_jointBody == null)
			return 0;

		if (IsRevoluteType())
		{
			return (index == -1) ? 0 : _jointBody.jointPosition[index];
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
		return (_jointBody == null || index == -1) ? 0 : _jointBody.jointForce[index];
	}

	/// <returns>in radian for angular and in meters for linear</param>
	public float GetJointVelocity(int index = 0)
	{
		index = GetValidIndex(index);
		var value = (_jointBody == null || index == -1) ? 0 : _jointBody.jointVelocity[index];
		return value;
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetForce(int index = 0)
	{
		index = GetValidIndex(index);
		var value = (_jointBody == null || index == -1) ? 0 : _jointBody.driveForce[index];
		return value;
	}

	/// <param name="driveType">ArticulationDriveType</param>
	/// <param name="targetVelocity">angular velocity in degrees per second</param>
	/// <param name="targetPosotion">target position </param>
	public void Drive(
		in float targetVelocity = 0,
		in float targetPosition = 0)
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
			_driveType == ArticulationDriveType.Target)
		{
			_jointBody.SetDriveTarget(drivceAxis, targetPosition);
		}

		if (_driveType == ArticulationDriveType.Acceleration ||
			_driveType == ArticulationDriveType.Force ||
			_driveType == ArticulationDriveType.Velocity)
		{
			var limitedTargetVelocity = GetLimitedVelocity(targetVelocity);
			_jointBody.SetDriveTargetVelocity(drivceAxis, limitedTargetVelocity);
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