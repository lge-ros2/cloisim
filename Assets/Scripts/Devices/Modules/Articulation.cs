/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class Articulation
{
	private ArticulationBody _jointBody = null;
	private ArticulationJointType _jointType = ArticulationJointType.FixedJoint;

	public ArticulationJointType Type => _jointType;

	private float _velocityLimit = float.NaN;

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

	public void Reset()
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
		return (Type == ArticulationJointType.RevoluteJoint || Type == ArticulationJointType.SphericalJoint) ? true : false;
	}

	public bool IsPrismaticType()
	{
		return (Type == ArticulationJointType.RevoluteJoint || Type == ArticulationJointType.PrismaticJoint) ? true : false;
	}

	private float GetLimitedVelocity(in float velocity)
	{
		if (!float.IsNaN(_velocityLimit) && Mathf.Abs(velocity) > Mathf.Abs(_velocityLimit))
		{
			return Mathf.Sign(velocity) * Mathf.Abs(_velocityLimit);
		}
		else
		{
			return velocity;
		}
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
			if (Type == ArticulationJointType.PrismaticJoint)
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
				Debug.LogWarning("Unsupported articulation Type: " + Type);
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

	/// <param name="target">angular velocity in degrees per second OR target position </param>
	public void Drive(in float target, ArticulationDriveType driveType = ArticulationDriveType.Velocity)
	{
		switch (driveType)
		{
			case ArticulationDriveType.Target:
				Drive(float.NaN, target);
				break;
			case ArticulationDriveType.Velocity:
				Drive(target, float.NaN);
				break;
			default:
				Debug.LogWarning("ArticulationDriveType should be Target/Velocity");
				return;
		}
	}

	/// <param name="targetVelocity">angular velocity in degrees per second.</param>
	/// <param name="target">target position </param>
	public void Drive(in float targetVelocity, in float targetPosition)
	{
		if (_jointBody == null)
		{
			Debug.LogWarning("ArticulationBody is empty, please set target body first");
			return;
		}

		if (targetVelocity == float.NaN && targetPosition == float.NaN)
		{
			Debug.LogWarning("Invalid Value: targetVelocity or targetPosition is NaN");
			return;
		}

		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).
		var drive = GetDrive();

		if (!float.IsNaN(targetVelocity) && !float.IsNaN(targetPosition))
		{
			drive.driveType = ArticulationDriveType.Force;
			// Debug.LogWarningFormat("targetVelocity={0} or targetPosition={1} Type={2}", targetVelocity, targetPosition, drive.driveType);
		}
		else if (float.IsNaN(targetVelocity) && !float.IsNaN(targetPosition))
		{
			drive.driveType = ArticulationDriveType.Target;
		}
		else if (!float.IsNaN(targetVelocity) && float.IsNaN(targetPosition))
		{
			drive.driveType = ArticulationDriveType.Velocity;
		}
		else
		{
			Debug.LogError("Invalid targetVelocity and targetPosition: Both NaN");
			return;
		}

		SetDrive(drive);

		var drivceAxis = GetDriveAxis();
		// Debug.LogWarningFormat("targetVelocity={0} targetPosition={1} Type={2}", targetVelocity, targetPosition, drive.driveType);

		if (drive.driveType == ArticulationDriveType.Force || drive.driveType == ArticulationDriveType.Target)
		{
			_jointBody.SetDriveTarget(drivceAxis, targetPosition);
		}

		if (drive.driveType == ArticulationDriveType.Force || drive.driveType == ArticulationDriveType.Velocity)
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
				Debug.LogWarning("GetDrive() unsupported joint type: " + _jointType);
				axis = ArticulationDriveAxis.X;
				break;
		}

		return axis;
	}

	private ArticulationDrive GetDrive()
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