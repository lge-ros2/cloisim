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
			private Model rootModel = null;
			private Model parentModelHelper = null;
			public bool drawInertia = false;

			private bool drawContact = true;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;

			public bool useGravity = false;

			private UE.ArticulationBody _artBody = null;

			public Dictionary<string, UE.ArticulationBody> jointList = new Dictionary<string, UE.ArticulationBody>();

			private List<UE.ContactPoint> contactPointList = new List<UE.ContactPoint>();

			public Model RootModel => rootModel;

			public Model Model => parentModelHelper;

			new void Awake()
			{
				base.Awake();
				parentModelHelper = transform.parent?.GetComponent<Model>();
			}

			// Start is called before the first frame update
			void Start()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				rootModel = (Model)modelHelpers[modelHelpers.Length - 1];

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

				if (drawContact && contactPointList.Count > 0)
				{
					// Debug-draw all contact points and normals
					foreach (var contact in contactPointList)
					{
						UE.Debug.DrawRay(contact.point, contact.normal, UE.Color.cyan, 0, true);
					}
					contactPointList.Clear();
				}
			}

			void OnJointBreak(float breakForce)
			{
				UE.Debug.Log("A joint has just been broken!, force: " + breakForce);
			}

			void OnCollisionStay(UE.Collision collisionInfo)
			{
				// Debug.Log(name + " |Stay| " + collisionInfo.gameObject.name);
				contactPointList.AddRange(collisionInfo.contacts);
			}

			private UE.Collider[] GetCollidersInChildren()
			{
				return GetComponentsInChildren<UE.Collider>();
			}

			private void IgnoreSelfCollision()
			{
				if (rootModel == null)
				{
					return;
				}

				var otherLinkPlugins = rootModel.GetComponentsInChildren<Link>();
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
