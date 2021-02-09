/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Link : Base
		{
			private Model _modelHelper = null;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;

			public Dictionary<string, UE.ArticulationBody> jointList = new Dictionary<string, UE.ArticulationBody>();

			new void Awake()
			{
				base.Awake();

				var modelObject = transform.parent;
				_modelHelper = modelObject.GetComponent<Model>();
			}

			// Start is called before the first frame update
			void Start()
			{
				// Handle self collision
				if (!isSelfCollide)
				{
					IgnoreSelfCollision(_modelHelper);
				}
				else
				{
					EnableSelfCollision();
				}

			}

			void OnJointBreak(float breakForce)
			{
				UE.Debug.Log("A joint has just been broken!, force: " + breakForce);
			}

			void OnCollisionStay(UE.Collision collisionInfo)
			{
				// Debug.Log(name + " |Stay| " + collisionInfo.gameObject.name);

				// Debug-draw all contact points and normals
				for (var index = 0; index < collisionInfo.contactCount; index++)
				{
					var contact = collisionInfo.contacts[index];
					UE.Debug.DrawRay(contact.point, contact.normal, UE.Color.blue);
					// Debug.Log(name + " |Stay| " + "," + contact.point + ", " + contact.separation.ToString("F5"));
				}
			}

			void OnCollisionEnter(UE.Collision collisionInfo)
			{
				// for (var index = 0; index < collisionInfo.contactCount; index++)
				// {
				// 	var contact = collisionInfo.contacts[index];
				// 	Debug.DrawRay(contact.point, contact.normal * 1.5f, Color.red, 0.1f);
				// 	// Debug.Log(name + " |Enter| " + "," + contact.point + ", " + contact.separation.ToString("F5"));
				// }
				// Debug.Log(name + " |Enter| " + collisionInfo.gameObject.name);
			}

			void OnCollisionExit(UE.Collision collisionInfo)
			{
				// Debug.Log(name + " |Exit| " + collisionInfo.gameObject.name);
			}

			private UE.Collider[] GetCollidersInChildren()
			{
				return GetComponentsInChildren<UE.Collider>();
			}

			private void IgnoreSelfCollision(in Model targetModelHelper)
			{
				if (targetModelHelper == null)
				{
					return;
				}

				var topParentModel = targetModelHelper.GetThisInTopParent();
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
							UE.Physics.IgnoreCollision(thisCollider, otherCollider);
							// Debug.Log("Ignore Collision(" + name + "): " + thisCollider.name + " <-> " + otherCollider.name);
						}
					}
				}
			}

			private void EnableSelfCollision()
			{
				var thisColliders = GetCollidersInChildren();

				foreach (var joint in GetComponentsInChildren<UE.Joint>())
				{
					joint.enableCollision = true;
				}
			}
		}
	}
}
