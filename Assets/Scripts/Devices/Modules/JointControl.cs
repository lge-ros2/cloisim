/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class JointControl
{
	protected ArticulationBody joint = null;

	protected ArticulationJointType jointType = ArticulationJointType.FixedJoint;

	public JointControl(in ArticulationBody joint)
	{
		this.joint = joint;
		this.jointType = this.joint.jointType;
	}

	public JointControl(in GameObject target)
	{
		var body = target.GetComponentInChildren<ArticulationBody>();
		this.joint = body;
		this.jointType = this.joint.jointType;
	}

	public void Reset()
	{
		this.joint.velocity = Vector3.zero;
		this.joint.angularVelocity = Vector3.zero;
	}

	protected void SetJointVelocity(in float velocity, in int targetDegree = 0)
	{
		if (this.joint != null)
		{
			var jointVelocity = this.joint.jointVelocity;
			jointVelocity[targetDegree] = velocity;
			this.joint.jointVelocity = jointVelocity;
		}
	}

	public float GetJointPosition(in int index = 0)
	{
		return (this.joint == null) ? 0 : this.joint.jointPosition[index];
	}

	public void Drive(in float effort, in float targetPosition, in float targetVelocity)
	{
		// targetVelocity angular velocity in degrees per second.
		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).
		var drive = DeviceHelper.GetDrive(ref this.joint);
		drive.damping = effort;
		drive.target = targetPosition;
		drive.targetVelocity = targetVelocity;
		DeviceHelper.SetDrive(ref this.joint, drive);
	}

	public void Drive(in float effort, in float targetVelocity)
	{
		// targetVelocity angular velocity in degrees per second.
		// Arccording to document(https://docs.unity3d.com/2020.3/Documentation/ScriptReference/ArticulationDrive.html)
		// F = stiffness * (currentPosition - target) - damping * (currentVelocity - targetVelocity).
		var drive = DeviceHelper.GetDrive(ref this.joint);
		drive.damping = effort;
		drive.targetVelocity = targetVelocity;
		DeviceHelper.SetDrive(ref this.joint, drive);
	}
}