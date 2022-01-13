/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Articulation
{
	public enum DriveType {NONE, FORCE_AND_VELOCITY, POSITION_AND_VELOCITY};
	private DriveType driveType = DriveType.NONE;

	private ArticulationBody _jointBody = null;
	private ArticulationJointType _jointType = ArticulationJointType.FixedJoint;

	public ArticulationJointType Type => _jointType;
	public GameObject gameObject => _jointBody.gameObject;

	public Articulation(in ArticulationBody jointBody)
	{
		if (jointBody != null)
		{
			_jointBody = jointBody;
			_jointType = _jointBody.jointType;
		}
	}

	public Articulation(in GameObject target)
		: this(target.GetComponentInChildren<ArticulationBody>())
	{
	}

	public void Reset()
	{
		_jointBody.velocity = Vector3.zero;
		_jointBody.angularVelocity = Vector3.zero;
	}

	public void SetDriveType(in DriveType type)
	{
		this.driveType = type;
	}

	public bool IsRevoluteType()
	{
		return (Type == ArticulationJointType.RevoluteJoint || Type == ArticulationJointType.SphericalJoint) ? true : false;
	}

	protected void SetJointVelocity(in float velocity, in int targetDegree = 0)
	{
		if (_jointBody != null)
		{
			var jointVelocity = _jointBody.jointVelocity;
			if (targetDegree < jointVelocity.dofCount)
			{
				jointVelocity[targetDegree] = velocity;
				_jointBody.jointVelocity = jointVelocity;
			}
		}
	}

	private int GetValidIndex(in int index)
	{
		return (index >= _jointBody.dofCount) ? (_jointBody.dofCount - 1) : index;
	}

	/// <returns>in radian for angular and in meters for linear</param>
	public float GetJointPosition(int index = 0)
	{
		index = GetValidIndex(index);
		return (_jointBody == null || index == -1) ? 0 : _jointBody.jointPosition[index];
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
		return (_jointBody == null || index == -1) ? 0 : _jointBody.jointVelocity[index];
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetEffort()
	{
		var drive = GetDrive();
		var F = drive.stiffness * (GetJointPosition() - drive.target) - drive.damping * (GetJointVelocity() - drive.targetVelocity);
		// Debug.Log(_jointBody.name + ": Calculated force = " + F);
		return F;
	}

	/// <param name="target">force or torque desired for FORCE_AND_VELOCITY type and position for POSITION_AND_VELOCITY.</param>
	/// <param name="targetVelocity">angular velocity in degrees per second.</param>
	public void Drive(in float target, in float targetVelocity)
	{
		if (_jointBody == null)
		{
			Debug.LogWarning("ArticulationBody is empty, please set target body first");
			return;
		}

		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).
		var drive = GetDrive();

		switch (this.driveType)
		{
			case DriveType.FORCE_AND_VELOCITY:
				drive.damping = target;
				break;
			case DriveType.POSITION_AND_VELOCITY:
				drive.target = target;
				break;
		}

		drive.targetVelocity = targetVelocity;

		SetDrive(drive);
	}

	public ArticulationDrive GetDrive()
	{
		ArticulationDrive drive;

		switch (_jointType)
		{
			case ArticulationJointType.RevoluteJoint:
				drive = _jointBody.xDrive;
				break;

			case ArticulationJointType.PrismaticJoint:

				if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
					_jointBody.linearLockY == ArticulationDofLock.LockedMotion)
				{
					drive = _jointBody.zDrive;
				}
				else if (_jointBody.linearLockY == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					drive = _jointBody.xDrive;
				}
				else if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					drive = _jointBody.yDrive;
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
					drive = _jointBody.xDrive;
				}
				else if (_jointBody.swingYLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					drive = _jointBody.zDrive;
				}
				else if (_jointBody.swingZLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					drive = _jointBody.yDrive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + _jointBody.name);
					goto default;
				}
				break;

			default:
				Debug.LogWarning("GetDrive() unsupported joint type: " + _jointType);
				drive = new ArticulationDrive();
				break;
		}

		return drive;
	}

	public void SetDrive(in ArticulationDrive drive)
	{
		switch (_jointType)
		{
			case ArticulationJointType.RevoluteJoint:
				_jointBody.xDrive = drive;
				break;

			case ArticulationJointType.PrismaticJoint:

				if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
					_jointBody.linearLockY == ArticulationDofLock.LockedMotion)
				{
					_jointBody.zDrive = drive;
				}
				else if (_jointBody.linearLockY == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					_jointBody.xDrive = drive;
				}
				else if (_jointBody.linearLockX == ArticulationDofLock.LockedMotion &&
						 _jointBody.linearLockZ == ArticulationDofLock.LockedMotion)
				{
					_jointBody.yDrive = drive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + _jointBody.name);
				}

				break;

			case ArticulationJointType.SphericalJoint:

				if (_jointBody.swingYLock == ArticulationDofLock.LockedMotion &&
					_jointBody.swingZLock == ArticulationDofLock.LockedMotion)
				{
					_jointBody.xDrive = drive;
				}
				else if (_jointBody.swingYLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					_jointBody.zDrive = drive;
				}
				else if (_jointBody.swingZLock == ArticulationDofLock.LockedMotion &&
						 _jointBody.twistLock == ArticulationDofLock.LockedMotion)
				{
					_jointBody.yDrive = drive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + _jointBody.name);
				}

				break;

			default:
				Debug.LogWarning("SetDrive() unsupported joint type: " + _jointType);
				break;
		}
	}
}