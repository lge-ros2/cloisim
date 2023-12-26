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
			private UE.ArticulationBody _artBody = null;

			public bool drawInertia = false;
			public bool drawContact = true;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;
			public bool useGravity = false;

			private string jointName = string.Empty;
			private string jointParentLinkName = string.Empty;
			private string jointChildLinkName = string.Empty;

			private UE.Vector3 jointAxis = UE.Vector3.zero;
			private UE.Vector3 jointAxis2 = UE.Vector3.zero;

			private float _jointAxisLimitVelocity = float.NaN;

			private float _jointAxis2LimitVelocity = float.NaN;

			private List<UE.ContactPoint> collisionContacts = new List<UE.ContactPoint>();

			private SensorDevices.Battery battery = null;
			public SensorDevices.Battery Battery => battery;

			public string JointName
			{
				get => this.jointName;
				set => this.jointName = value;
			}

			public string JointParentLinkName
			{
				get => this.jointParentLinkName;
				set => this.jointParentLinkName = value;
			}

			public string JointChildLinkName
			{
				get => this.jointChildLinkName;
				set => this.jointChildLinkName = value;
			}

			public float JointAxisLimitVelocity
			{
				get => this._jointAxisLimitVelocity;
				set => this._jointAxisLimitVelocity = value;
			}

			public float JointAxis2LimitVelocity
			{
				get => this._jointAxis2LimitVelocity;
				set => this._jointAxis2LimitVelocity = value;
			}

			public UE.Vector3 JointAxis
			{
				get => this.jointAxis;
				set => this.jointAxis = value;
			}

			public UE.Vector3 JointAxis2
			{
				get => this.jointAxis2;
				set => this.jointAxis2 = value;
			}

			public Model RootModel => rootModel;

			public Model Model => parentModelHelper;

			new protected void Awake()
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

			void LateUpdate()
			{
				SetPose(transform.localPosition, transform.localRotation, 1);
			}

			void OnDrawGizmos()
			{
				if (_artBody && drawInertia)
				{
					UE.Gizmos.color = new UE.Color(0.35f, 0.0f, 0.1f, 0.1f);

					var region = _artBody.inertiaTensor;
					if (region.x < 1f && region.y < 1f && region.z < 1f)
					{
						region.Set(region.magnitude / region.x, region.magnitude / region.y, region.magnitude / region.z);
					}
					region = region.normalized;

					UE.Gizmos.DrawCube(transform.position, region);
				}

				lock (this.collisionContacts)
				{
					if (drawContact && collisionContacts != null && collisionContacts.Count > 0)
					{
						var contactColor = UE.Color.cyan;
						contactColor.b = UE.Random.Range(0.5f, 1.0f);

						// Debug-draw all contact points and normals
						for (var i = 0; i < collisionContacts.Count; i++)
						{
							var contact = collisionContacts[i];
							UE.Debug.DrawRay(contact.point, contact.normal, contactColor);
						}
						collisionContacts.Clear();
					}
				}
			}

			void OnJointBreak(float breakForce)
			{
				UE.Debug.Log("A joint has just been broken!, force: " + breakForce);
			}

#if UNITY_EDITOR
			void OnCollisionStay(UE.Collision collisionInfo)
			{
				lock (this.collisionContacts)
				{
					// UE.Debug.Log(name + " |Stay| " + collisionInfo.gameObject.name);
					collisionInfo.GetContacts(this.collisionContacts);
				}
			}
#endif

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

			public void AttachBattery(in string name, in float initVoltage)
			{
				if (battery == null)
				{
					battery = new SensorDevices.Battery(name);
				}

				battery.SetMax(initVoltage);
			}
		}
	}
}
