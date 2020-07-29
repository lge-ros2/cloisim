/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public partial class SDFImporter : SDF.Importer
{
	private static GameObject FindObjectByName(string name, Transform target)
	{
		GameObject objectFound = null;

		if (name.Contains("::"))
		{
			name = name.Replace("::", ":");
			var tmp = name.Split(':');
			if (tmp.Length == 2)
			{
				var obj = target.Find(tmp[0]);
				if (obj != null)
				{
					var obj2 = obj.Find(tmp[1]);
					if (obj2 != null)
						objectFound = obj2.gameObject;
				}
			}
		}
		else
		{
			var obj = target.Find(name);
			if (obj != null)
			{
				objectFound = obj.gameObject;
			}
		}

		return objectFound;
	}

	protected override void ImportJoint(in SDF.Joint joint, in System.Object parentObject)
	{
		var linkNameParent = joint.ParentLinkName;
		var linkNameChild = joint.ChildLinkName;

		// Debug.LogFormat("[Joint] {0}, Connection: {1} <= {2}", joint.Name, linkNameParent, linkNameChild);

		var transformParent = (parentObject as GameObject).transform;

		var linkObjectParent = FindObjectByName(linkNameParent, transformParent);
		var linkObjectChild = FindObjectByName(linkNameChild, transformParent);

		if (linkObjectChild is null || linkObjectParent is null)
		{
			Debug.LogErrorFormat("Link object is NULL!!! child({0}) parent({1})", linkObjectChild, linkObjectParent);
			return;
		}

		var rigidBodyChild = linkObjectChild.GetComponent<Rigidbody>();
		var rigidBodyParent = linkObjectParent.GetComponent<Rigidbody>();

		if (rigidBodyChild is null || rigidBodyParent is null)
		{
			Debug.LogErrorFormat("RigidBody of Link is NULL!!! child({0}) parent({1})", rigidBodyChild, rigidBodyParent);
			return;
		}

		Joint jointComponent = null;

		if (joint.Type.Equals("ball"))
		{
			var ballJointComponent = SDFImplement.Joint.AddBall(linkObjectChild, rigidBodyParent);
			jointComponent = ballJointComponent as Joint;
		}
		else if (joint.Type.Equals("prismatic"))
		{
			var prismaticJointComponent = SDFImplement.Joint.AddPrismatic(joint.Axis, joint.Pose, linkObjectChild, rigidBodyParent);
			jointComponent = prismaticJointComponent as Joint;
		}
		else if (joint.Type.Equals("revolute"))
		{
			var revoluteJointComponent = SDFImplement.Joint.AddRevolute(joint.Axis, linkObjectChild, rigidBodyParent);
			jointComponent = revoluteJointComponent as Joint;
		}
		else if (joint.Type.Equals("revolute2"))
		{
			var revolute2JointComponent = SDFImplement.Joint.AddRevolute2(joint.Axis, joint.Axis2, linkObjectChild, rigidBodyParent);
			jointComponent = revolute2JointComponent as Joint;
		}
		else if (joint.Type.Equals("fixed"))
		{
			var fixedJointComponent = SDFImplement.Joint.AddFixed(linkObjectChild, rigidBodyParent);
			jointComponent = fixedJointComponent as Joint;
		}
		else if (joint.Type.Equals("gearbox"))
		{
			// gearbox_ratio = GetValue<double>("gearbox_ratio");
			// gearbox_reference_body = GetValue<string>("gearbox_reference_body");
			Debug.LogWarning("This type[gearbox] is not supported now.");
		}
		else if (joint.Type.Equals("screw"))
		{
			// thread_pitch = GetValue<double>("thread_pitch");
			Debug.LogWarning("This type[screw] is not supported now.");
		}
		else
		{
			Debug.LogWarningFormat("Check Joint type[{0}]", joint.Type);
		}

		if (jointComponent != null)
		{
			SDFImplement.Joint.SetCommonConfiguration(jointComponent, joint.Pose.Pos, linkObjectChild);
		}
	}
}
