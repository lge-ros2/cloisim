/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public partial class SDFImporter : SDF.Importer
{
	private static float minimumInertiaTensor = 1e-6f;

	private Vector3 GetInertiaTensor(in SDF.Inertial inertia)
	{
		// Debug.LogWarningFormat("GetInertiaTensor: {0}, {1}, {2}", inertia.ixx, inertia.iyy, inertia.izz);
		var inertiaVector = SDF2Unity.GetPosition(inertia.ixx, inertia.iyy, inertia.izz);

		if (inertiaVector.x <= minimumInertiaTensor)
		{
			inertiaVector.x = minimumInertiaTensor;
		}

		if (inertiaVector.y <= minimumInertiaTensor)
		{
			inertiaVector.y = minimumInertiaTensor;
		}

		if (inertiaVector.z <= minimumInertiaTensor)
		{
			inertiaVector.z = minimumInertiaTensor;
		}

		return inertiaVector;
	}

	protected override System.Object ImportLink(in SDF.Link link, in System.Object parentObject)
	{
		var targetObject = (parentObject as GameObject);
		var newLinkObject = new GameObject(link.Name);
		SetParentObject(newLinkObject, targetObject);

		var linkPlugin = newLinkObject.AddComponent<LinkPlugin>();
		linkPlugin.isSelfCollide = link.SelfCollide;

		var localPosition = SDF2Unity.GetPosition(link.Pose.Pos);
		var localRotation = SDF2Unity.GetRotation(link.Pose.Rot);
		linkPlugin.SetPose(localPosition, localRotation);

		return newLinkObject as System.Object;
	}

	protected override void PostImportLink(in SDF.Link link, in System.Object targetObject)
	{
		var linkObject = (targetObject as GameObject);

		if (linkObject == null)
		{
			Debug.LogError("Link Object is null: " + link.Name);
			return;
		}

		var disableConvex = false;

		if (link.Inertial != null)
		{
			var rigidBody = linkObject.AddComponent<Rigidbody>(); // Add the rigidbody.

			foreach (var collider in linkObject.GetComponentsInChildren<Collider>())
			{
				if (collider.attachedRigidbody == null)
				{
					Debug.LogWarningFormat(linkObject.name + " > " + collider.name + " [=] null Rigidbody ");
				}
			}

			rigidBody.velocity = Vector3.zero;
			rigidBody.angularVelocity = Vector3.zero;
			rigidBody.drag = 0.05f;
			rigidBody.angularDrag = 0.10f;

			rigidBody.useGravity = link.Gravity;
			rigidBody.isKinematic = link.Kinematic;

			rigidBody.mass = (float)(link.Inertial.mass);

			// rigidBody.ResetCenterOfMass();
			// rigidBody.ResetInertiaTensor();
			rigidBody.centerOfMass = SDF2Unity.GetPosition(link.Inertial.pose.Pos);
			// rigidBody.inertiaTensor = GetInertiaTensor(link.Inertial);
			// rigidBody.inertiaTensorRotation = Quaternion.identity;
			// Debug.Log(rigidBody.name + " => Center Of Mass: " + rigidBody.centerOfMass.ToString("F6") + ", intertia: " + rigidBody.inertiaTensor.ToString("F6") + ", " + rigidBody.inertiaTensorRotation.ToString("F6"));
		}
		else
		{
			// If the child does not have rigidbody, collider of child would disable convex.
			disableConvex = true;
		}

		if (disableConvex)
		{
			// Sholud be handled after set parent object!!
			var meshColliders = linkObject.GetComponentsInChildren<MeshCollider>();
			foreach (var meshCollider in meshColliders)
			{
				meshCollider.convex = false;
				// Debug.LogWarning("Make convex false:" + meshCollider.name);
			}
		}
	}
}
