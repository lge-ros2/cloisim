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

	private ArticulationBody jointBody = null;
	private ArticulationJointType jointType = ArticulationJointType.FixedJoint;

	public ArticulationJointType Type => jointType;
	public GameObject gameObject => this.jointBody.gameObject;

	public Articulation(in ArticulationBody jointBody)
	{
		this.jointBody = jointBody;
		this.jointType = this.jointBody.jointType;
	}

	public Articulation(in GameObject target)
	{
		var body = target.GetComponentInChildren<ArticulationBody>();
		this.jointBody = body;
		this.jointType = this.jointBody.jointType;
	}

	public void Reset()
	{
		this.jointBody.velocity = Vector3.zero;
		this.jointBody.angularVelocity = Vector3.zero;
	}

	public void SetDriveType(in DriveType type)
	{
		this.driveType = type;
	}

	public bool IsRevoluteType()
	{
		return (Type.Equals(ArticulationJointType.RevoluteJoint) || Type.Equals(ArticulationJointType.SphericalJoint)) ? true : false;
	}

	protected void SetJointVelocity(in float velocity, in int targetDegree = 0)
	{
		if (this.jointBody != null)
		{
			var jointVelocity = this.jointBody.jointVelocity;
			jointVelocity[targetDegree] = velocity;
			this.jointBody.jointVelocity = jointVelocity;
		}
	}

	private int GetValidIndex(in int index)
	{
		return (index >= this.jointBody.dofCount) ? (this.jointBody.dofCount - 1) : index;
	}

	/// <returns>in radian for angular and in meters for linear</param>
	public float GetJointPosition(int index = 0)
	{
		index = GetValidIndex(index);
		return (this.jointBody == null || index == -1) ? 0 : this.jointBody.jointPosition[index];
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetJointForce(int index = 0)
	{
		index = GetValidIndex(index);
		// Debug.Log(this.jointBody.name + ": " + this.jointBody.dofCount + ", " + this.jointBody.jointAcceleration[0] + ", " + this.jointBody.jointForce[0]);
		return (this.jointBody == null || index == -1) ? 0 : this.jointBody.jointForce[index];
	}

	/// <returns>in radian for angular and in meters for linear</param>
	public float GetJointVelocity(int index = 0)
	{
		index = GetValidIndex(index);
		return (this.jointBody == null || index == -1) ? 0 : this.jointBody.jointVelocity[index];
	}

	/// <returns>torque for angular and force for linear</param>
	public float GetEffort()
	{
		var drive = DeviceHelper.GetDrive(ref this.jointBody);
		var F = drive.stiffness * (GetJointPosition() - drive.target) - drive.damping * (GetJointVelocity() - drive.targetVelocity);
		// Debug.Log(this.jointBody.name + ": Calculated force = " + F);
		return F;
	}

	/// <param name="target">force or torque desired for FORCE_AND_VELOCITY type and position for POSITION_AND_VELOCITY.</param>
	/// <param name="targetVelocity">angular velocity in degrees per second.</param>
	public void Drive(in float target, in float targetVelocity)
	{
		if (this.jointBody == null)
		{
			Debug.LogWarning("ArticulationBody is empty, please set target body first");
			return;
		}

		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).
		var drive = DeviceHelper.GetDrive(ref this.jointBody);

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

		DeviceHelper.SetDrive(ref this.jointBody, drive);
	}
}