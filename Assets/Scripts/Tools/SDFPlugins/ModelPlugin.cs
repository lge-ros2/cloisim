/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class ModelPlugin : MonoBehaviour
{
	[Header("SDF Properties")]
	public bool isStatic = false;

	private bool isTopModel = false;

	public bool IsTopModel => isTopModel;

	private PoseControl poseControl = new PoseControl();

	public ModelPlugin GetThisInTopParent()
	{
		var modelPlugins = GetComponentsInParent(typeof(ModelPlugin));
		return (ModelPlugin)modelPlugins[modelPlugins.Length - 1];
	}

	public LinkPlugin[] GetLinksInChildren()
	{
		return GetComponentsInChildren<LinkPlugin>();
	}

	void Awake()
	{
		tag = "Model";
		isTopModel = SDF2Unity.CheckTopModel(transform);
		poseControl.SetTransform(transform);
	}

	private bool MakeBridgeJoint(Rigidbody targetRigidBody)
	{
		if (GetComponent<Rigidbody>() != null)
		{
			return false;
		}

		// Configure rigidbody for root object
		var rigidBody = gameObject.AddComponent<Rigidbody>();
		rigidBody.mass = 0.0001f;
		rigidBody.drag = 0;
		rigidBody.angularDrag = 0;
		rigidBody.useGravity = false;
		rigidBody.isKinematic = false;
		rigidBody.ResetCenterOfMass();
		rigidBody.ResetInertiaTensor();

		var fixedJoint = gameObject.AddComponent<FixedJoint>();
		fixedJoint.connectedBody = targetRigidBody;
		fixedJoint.enableCollision = false;
		fixedJoint.enablePreprocessing = false;
		fixedJoint.massScale = 1;
		fixedJoint.connectedMassScale = 1;

		return true;
	}

	private void FindAndMakeBridgeJoint()
	{
		var rigidBodyChildren = GetComponentsInChildren<Rigidbody>();
		foreach (var rigidBodyChild in rigidBodyChildren)
		{
			// Get child component in only first depth!!!
			// And make bridge joint
			if (rigidBodyChild != null && rigidBodyChild.transform.parent == this.transform)
			{
				if (MakeBridgeJoint(rigidBodyChild) == true)
				{
					break;
				}
			}
		}
	}

	// Start is called before the first frame update
	void Start()
	{
		if (isTopModel)
		{
			FindAndMakeBridgeJoint();
		}
	}

	public void Reset()
	{
		poseControl.Reset();
	}

	public void SetPose(in Vector3 position, in Quaternion rotation)
	{
		AddPose(position, rotation);
		Reset();
	}

	public void AddPose(in Vector3 position, in Quaternion rotation)
	{
		poseControl.Add(position, rotation);
	}
}