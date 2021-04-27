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
			private Model _topModel = null;
			private Model _modelHelper = null;
			public bool drawInertia = false;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;

			public bool useGravity = false;

			private UE.ArticulationBody _artBody = null;

			public Dictionary<string, UE.ArticulationBody> jointList = new Dictionary<string, UE.ArticulationBody>();

			public Model TopModel => _topModel;

			public Model Model => _modelHelper;

			new void Awake()
			{
				base.Awake();
				_modelHelper = transform.parent?.GetComponent<Model>();
			}

			// Start is called before the first frame update
			void Start()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				_topModel = (Model)modelHelpers[modelHelpers.Length - 1];

				_artBody = GetComponent<UE.ArticulationBody>();

				// Handle self collision
				if (!isSelfCollide)
				{
					IgnoreSelfCollision();
				}
			}

			void OnDrawGizmos()
			{
				if (_artBody && drawInertia)
				{
					UE.Gizmos.color = new UE.Color(0.35f, 0.0f, 0.1f, 0.1f);

					var region = _artBody.inertiaTensor;
					if (region.x < 1f && region.y < 1f && region.z < 1f)
					{
						region.Set(region.magnitude/region.x, region.magnitude/region.y, region.magnitude/region.z);
					}
					region = region.normalized;

					UE.Gizmos.DrawCube(transform.position, region);
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

			private void IgnoreSelfCollision()
			{
				if (_topModel == null)
				{
					return;
				}

				var otherLinkPlugins = _topModel.GetComponentsInChildren<Link>();
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
		}
	}
}
