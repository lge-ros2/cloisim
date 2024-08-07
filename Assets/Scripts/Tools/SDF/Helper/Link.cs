/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Link : Base
		{
			private Model _rootModel = null;
			private Model _parentModelHelper = null;
			private UE.ArticulationBody _artBody = null;
			private UE.ArticulationBody _parentArtBody = null;
			private Link _parentLink = null;
			private UE.Pose _jointPose = UE.Pose.identity;
			private bool _isParentLinkModel = false;

			[UE.Header("Properties")]
			public bool drawInertia = false;
			public bool drawContact = true;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;
			public bool useGravity = false;

			[UE.Header("Joint related")]
			private string jointName = string.Empty;
			private string jointParentLinkName = string.Empty;
			private string jointChildLinkName = string.Empty;

			private UE.Pose _jointAnchorPose = new UE.Pose();

			private float _jointAxisLimitVelocity = float.NaN;
			private float _jointAxis2LimitVelocity = float.NaN;

			private List<UE.ContactPoint> collisionContacts = new List<UE.ContactPoint>();

			private SensorDevices.Battery _battery = null;
			public SensorDevices.Battery Battery => _battery;

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

			public UE.Pose LinkJointPose => _jointPose;

			public Model RootModel => _rootModel;
			public Model Model => _parentModelHelper;

			new protected void Awake()
			{
				base.Awake();
				_parentModelHelper = transform.parent?.GetComponent<Model>();
				_jointAnchorPose = UE.Pose.identity;
			}

			// Start is called before the first frame update
			void Start()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				_rootModel = (Model)modelHelpers[modelHelpers.Length - 1];

				var parentArtBodies = GetComponentsInParent<UE.ArticulationBody>();

				if (parentArtBodies.Length > 0)
				{
					_artBody = parentArtBodies[0];

					if (parentArtBodies.Length > 1)
					{
						_parentArtBody = parentArtBodies[1];
						_parentLink = _parentArtBody.gameObject.GetComponent<Link>();
					}
				}

				if (transform.parent.CompareTag("Link"))
				{
					_isParentLinkModel = true;
				}

				// Handle self collision
				if (!isSelfCollide)
				{
					IgnoreSelfCollision();
				}

				StartCoroutine(HandleTerrainSize());
			}

			void LateUpdate()
			{
				SetPose(transform.localPosition, transform.localRotation, 1);

				if (_artBody != null)
				{
					_jointAnchorPose.position = _artBody.anchorPosition;
					_jointAnchorPose.rotation = _artBody.anchorRotation;
				}

				_jointPose.position = transform.localPosition + _jointAnchorPose.position;
				_jointPose.rotation = transform.localRotation * _jointAnchorPose.rotation;

				if (_parentLink != null && _isParentLinkModel == false)
				{
					_jointPose.position -= _parentLink._jointPose.position;
				}
		}

			void OnDrawGizmos()
			{
				if (_artBody && drawInertia)
				{
					UE.Gizmos.color = new UE.Color(0.45f, 0.1f, 0.15f, 0.3f);

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

			private IEnumerator HandleTerrainSize()
			{
				yield return new UE.WaitForEndOfFrame();

				var terrain = GetComponentInChildren<UE.Terrain>();
				if (terrain != null)
				{
					// UE.Debug.LogWarning("due to bug gof Terrain. Re-set the size of terrain data in Terrain");
					var size = terrain.terrainData.size;
					terrain.terrainData.size = size;
				}
			}

			private UE.Collider[] GetCollidersInChildren()
			{
				return GetComponentsInChildren<UE.Collider>();
			}

			private void IgnoreSelfCollision()
			{
				if (_rootModel == null)
				{
					return;
				}

				var otherLinkPlugins = _rootModel.GetComponentsInChildren<Link>();
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
				if (_battery == null)
				{
					_battery = new SensorDevices.Battery(name);
				}

				_battery.SetMax(initVoltage);
			}
		}
	}
}
