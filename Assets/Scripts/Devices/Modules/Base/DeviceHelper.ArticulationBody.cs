/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class DeviceHelper
{
	public static ArticulationDrive GetDrive(ref ArticulationBody body)
	{
		ArticulationDrive drive;

		var type = body.jointType;

		switch (type)
		{
			case ArticulationJointType.RevoluteJoint:

				drive = body.xDrive;

				break;

			case ArticulationJointType.PrismaticJoint:

				if (body.linearLockX.Equals(ArticulationDofLock.LockedMotion) &&
					body.linearLockY.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.zDrive;
				}
				else if (body.linearLockY.Equals(ArticulationDofLock.LockedMotion) &&
						 body.linearLockZ.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.xDrive;
				}
				else if (body.linearLockX.Equals(ArticulationDofLock.LockedMotion) &&
						 body.linearLockZ.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.yDrive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + body.name);
					drive = new ArticulationDrive();
				}

				break;

			case ArticulationJointType.SphericalJoint:

				if (body.swingYLock.Equals(ArticulationDofLock.LockedMotion) &&
					body.swingZLock.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.xDrive;
				}
				else if (body.swingYLock.Equals(ArticulationDofLock.LockedMotion) &&
						 body.twistLock.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.zDrive;
				}
				else if (body.swingZLock.Equals(ArticulationDofLock.LockedMotion) &&
						 body.twistLock.Equals(ArticulationDofLock.LockedMotion))
				{
					drive = body.yDrive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + body.name);
					drive = new ArticulationDrive();
				}

				break;

			default:
				Debug.LogWarning("unsupported joint type");
				drive = new ArticulationDrive();
				break;
		}

		return drive;
	}

	public static void SetDrive(ref ArticulationBody body, in ArticulationDrive drive)
	{
		var type = body.jointType;

		switch (type)
		{
			case ArticulationJointType.RevoluteJoint:

				body.xDrive = drive;

				break;

			case ArticulationJointType.PrismaticJoint:

				if (body.linearLockX.Equals(ArticulationDofLock.LockedMotion) &&
					body.linearLockY.Equals(ArticulationDofLock.LockedMotion))
				{
					body.zDrive = drive;
				}
				else if (body.linearLockY.Equals(ArticulationDofLock.LockedMotion) &&
						 body.linearLockZ.Equals(ArticulationDofLock.LockedMotion))
				{
					body.xDrive = drive;
				}
				else if (body.linearLockX.Equals(ArticulationDofLock.LockedMotion) &&
						 body.linearLockZ.Equals(ArticulationDofLock.LockedMotion))
				{
					body.yDrive = drive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + body.name);
				}

				break;

			case ArticulationJointType.SphericalJoint:

				if (body.swingYLock.Equals(ArticulationDofLock.LockedMotion) &&
					body.swingZLock.Equals(ArticulationDofLock.LockedMotion))
				{
					body.xDrive = drive;
				}
				else if (body.swingYLock.Equals(ArticulationDofLock.LockedMotion) &&
						 body.twistLock.Equals(ArticulationDofLock.LockedMotion))
				{
					body.zDrive = drive;
				}
				else if (body.swingZLock.Equals(ArticulationDofLock.LockedMotion) &&
						 body.twistLock.Equals(ArticulationDofLock.LockedMotion))
				{
					body.yDrive = drive;
				}
				else
				{
					Debug.LogWarning("Wrong Joint configuration!!! -> " + body.name);
				}

				break;

			default:
				Debug.LogWarning("unsupported joint type");
				break;
		}
	}

}