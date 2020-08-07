/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class LinkPlugin : MonoBehaviour
{
	public bool isSelfCollide = false;

	private ModelPlugin modelPlugin = null;

	private PoseControl poseControl = null;

	public Dictionary<string, Joint> jointList;

	void Awake()
	{
		poseControl = new PoseControl();
		jointList = new Dictionary<string, Joint>();

		tag = "Link";
		var modelObject = transform.parent;
		modelPlugin = modelObject.GetComponent<ModelPlugin>();
		poseControl.SetTransform(transform);
	}

	// Start is called before the first frame update
	void Start()
	{
		RemoveFixedJointAndRigidBodyParts();

		// Handle self collision
		if (!isSelfCollide)
		{
			IgnoreSelfCollision(modelPlugin);
		}
		else
		{
			EnableSelfCollision();
		}

		// if parent model has static option, make it all static in child
		if (modelPlugin != null)
		{
			if (modelPlugin.isStatic)
			{
				MakeStaticLink();
			}
		}

		ScaleMassOnJoint();
	}

	void OnJointBreak(float breakForce)
	{
		Debug.Log("A joint has just been broken!, force: " + breakForce);
	}

	void OnCollisionStay(Collision collisionInfo)
	{
		// Debug.Log(name + " |Stay| " + collisionInfo.gameObject.name);

		// Debug-draw all contact points and normals
		for (var index = 0; index < collisionInfo.contactCount; index++)
		{
			var contact = collisionInfo.contacts[index];
			Debug.DrawRay(contact.point, contact.normal * 1.2f, Color.blue);
			// Debug.Log(name + " |Stay| " + "," + contact.point + ", " + contact.separation.ToString("F5"));
		}
	}

	void OnCollisionEnter(Collision collisionInfo)
	{
		// for (var index = 0; index < collisionInfo.contactCount; index++)
		// {
		// 	var contact = collisionInfo.contacts[index];
		// 	Debug.DrawRay(contact.point, contact.normal * 1.5f, Color.red, 0.1f);
		// 	// Debug.Log(name + " |Enter| " + "," + contact.point + ", " + contact.separation.ToString("F5"));
		// }
		// Debug.Log(name + " |Enter| " + collisionInfo.gameObject.name);
	}

	void OnCollisionExit(Collision collisionInfo)
	{
		// Debug.Log(name + " |Exit| " + collisionInfo.gameObject.name);
	}

	private Collider[] GetCollidersInChildren()
	{
		return GetComponentsInChildren<Collider>();
	}

	private void IgnoreSelfCollision(in ModelPlugin targetModelPlugin)
	{
		if (targetModelPlugin == null)
		{
			return;
		}

		var topParentModel = targetModelPlugin.GetThisInTopParent();
		var otherLinkPlugins = topParentModel.GetLinksInChildren();
		var thisColliders = GetCollidersInChildren();

		foreach (var otherLinkPlugin in otherLinkPlugins)
		{
			if (otherLinkPlugin.Equals(this))
			{
				// Debug.LogWarningFormat("Skip, this Component{0} is same!!!", name);
				continue;
			}

			foreach (var otherCollider in otherLinkPlugin.GetCollidersInChildren())
			{
				foreach (var thisCollider in thisColliders)
				{
					Physics.IgnoreCollision(thisCollider, otherCollider);
					// Debug.Log("Ignore Collision(" + name + "): " + thisCollider.name + " <-> " + otherCollider.name);
				}
			}
		}
	}

	private void EnableSelfCollision()
	{
		var thisColliders = GetCollidersInChildren();

		foreach (var joint in GetComponentsInChildren<Joint>())
		{
			joint.enableCollision = true;
		}
	}

	private void MakeStaticLink()
	{
		foreach (var child in GetComponentsInChildren<Transform>())
		{
			child.gameObject.isStatic = true;
			var childRigidBody = child.GetComponentInChildren<Rigidbody>();
			if (childRigidBody != null)
			{
				childRigidBody.isKinematic = true;
			}
		}
	}

	private void ScaleMassOnJoint()
	{
		var thisJoints = this.GetComponents<Joint>();
		foreach (var thisJoint in thisJoints)
		{
			if (thisJoint && thisJoint.GetType() != typeof(FixedJoint))
			{
				var connectedBody = thisJoint.connectedBody;
				var connectedBodyObject = thisJoint.connectedBody.gameObject;

				if (connectedBody && connectedBodyObject && connectedBodyObject.tag.Equals("Link"))
				{
					var thisBody = GetComponent<Rigidbody>();

					if (connectedBody.mass > thisBody.mass)
					{
						thisJoint.massScale = 1;
						thisJoint.connectedMassScale = connectedBody.mass / thisBody.mass;
					}
					else
					{
						thisJoint.massScale = thisBody.mass / connectedBody.mass;
						thisJoint.connectedMassScale = 1;
					}
				}
			}
		}
	}

	private void RemoveFixedJointAndRigidBodyParts()
	{
		if (TryGetComponent<FixedJoint>(out var fixedJoint))
		{
			GameObject.Destroy(fixedJoint);

			if (TryGetComponent<Rigidbody>(out var rigidbody))
			{
				rigidbody.isKinematic = true;
				GameObject.Destroy(rigidbody);
				transform.localPosition = Vector3.zero;
				transform.localRotation = Quaternion.identity;

				Debug.LogWarningFormat("[{0}] fixedJoint including Rigidbody will not be implemented as a joint. " +
								"Only transform parenting for better performance", name);
			}
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