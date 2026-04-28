/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Helper
	{
		public class Link : Base
		{
			private Model _parentModelHelper = null;
			private UE.ArticulationBody _artBody = null;
			private UE.ArticulationBody _parentArtBody = null;
			private Link _parentLink = null;
			private UE.Pose _jointPose = UE.Pose.identity;
			private bool _isParentLinkModel = false;

			[UE.Header("Properties")]
			public bool drawInertia = false;
			public bool drawCenterOfMass = true;
			public bool drawContact = true;

			[UE.Header("SDF Properties")]
			public bool isSelfCollide = false;
			public bool useGravity = true;
			public bool autoInertia = false;

			[UE.Header("Joint related")]
			private string jointName = string.Empty;
			private string jointParentLinkName = string.Empty;
			private string jointChildLinkName = string.Empty;

			private UE.Pose _jointAnchorPose = new UE.Pose();

#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
			private float _jointAxisLimitVelocity = float.NaN;
			private float _jointAxis2LimitVelocity = float.NaN;
#endif

			private MimicConstraint _jointAxisMimic = null;
			private MimicConstraint _jointAxis2Mimic = null;

			private List<UE.ContactPoint> collisionContacts = new();

			private SensorDevices.Battery _battery = null;
			public SensorDevices.Battery Battery => _battery;

			public string JointName
			{
				get => jointName;
				set => jointName = value;
			}

			public string JointParentLinkName
			{
				get => jointParentLinkName;
				set => jointParentLinkName = value;
			}

			public string JointChildLinkName
			{
				get => jointChildLinkName;
				set => jointChildLinkName = value;
			}
#if true // TODO: Candidate to remove due to AriticulationBody.maxJointVelocity
			public float JointAxisLimitVelocity
			{
				get => _jointAxisLimitVelocity;
				set => _jointAxisLimitVelocity = value;
			}

			public float JointAxis2LimitVelocity
			{
				get => _jointAxis2LimitVelocity;
				set => _jointAxis2LimitVelocity = value;
			}
#endif

			public MimicConstraint JointAxisMimic
			{
				get => _jointAxisMimic;
				set => _jointAxisMimic = value;
			}

			public MimicConstraint JointAxis2Mimic
			{
				get => _jointAxis2Mimic;
				set => _jointAxis2Mimic = value;
			}

			public UE.Pose LinkJointPose => _jointPose;

			public Model Model => _parentModelHelper;

			new protected void Awake()
			{
				base.Awake();
				_parentModelHelper = transform.parent?.GetComponent<Model>();
				_jointAnchorPose = UE.Pose.identity;
			}

			// Start is called before the first frame update
			new protected void Start()
			{
				base.Start();

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

				var artBodies = (RootModel != null)
					? RootModel.GetComponentsInChildren<UE.ArticulationBody>()
					: GetComponentsInChildren<UE.ArticulationBody>();
				foreach (var ab in artBodies)
					_totalMass += ab.mass;
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

			[UE.SerializeField]
			private float _sizeCoM = 0.03f;
			private float _totalMass = 0f;
			private void DrawInertiaAndCoM(UE.ArticulationBody artBody, in float totalMass)
			{
				if (_artBody == null)
					return;

				if (drawInertia)
				{
					// Draw inertia tensor
					UE.Gizmos.color = new UE.Color(0.45f, 0.1f, 0.15f, 0.2f);

					var region = artBody.inertiaTensor;
					if (region.x < 1f && region.y < 1f && region.z < 1f)
					{
						region.Set(region.magnitude / region.x, region.magnitude / region.y, region.magnitude / region.z);
					}
					region = region.normalized;

					UE.Gizmos.DrawCube(artBody.transform.position, region);
				}

				if (drawCenterOfMass)
				{
					// Draw center of mass
					// Sphere size scales with mass share: min=25% of _sizeCoM, max=_sizeCoM (ratio=1)
					var massRatio = (totalMass > 0f) ? artBody.mass / totalMass : 0f;
					var comSphereSize = UE.Mathf.Lerp(_sizeCoM * 0.25f, _sizeCoM, massRatio);
					UE.Gizmos.color = new UE.Color(1f, 1f, 1f, 0.9f);
					var comWorld = artBody.transform.TransformPoint(artBody.centerOfMass);
					UE.Gizmos.DrawSphere(comWorld, comSphereSize);

					UE.Gizmos.color = new UE.Color(0.1f, 0.8f, 0.2f, 0.3f);
					UE.Gizmos.DrawWireSphere(comWorld, _sizeCoM);
				}
			}

			void OnDrawGizmosSelected()
			{
				var childArtBodies = GetComponentsInChildren<UE.ArticulationBody>();

				foreach (var childArtBody in childArtBodies)
				{
					DrawInertiaAndCoM(childArtBody, _totalMass);
				}

				if (drawContact)
				{
					lock (collisionContacts)
					{
						if (collisionContacts != null && collisionContacts.Count > 0)
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
			}

			void OnJointBreak(float breakForce)
			{
				UE.Debug.Log("A joint has just been broken!, force: " + breakForce);
			}

#if UNITY_EDITOR
			void OnCollisionStay(UE.Collision collisionInfo)
			{
				lock (collisionContacts)
				{
					// UE.Debug.Log(name + " |Stay| " + collisionInfo.gameObject.name);
					collisionInfo.GetContacts(collisionContacts);
				}
			}
#endif

			private UE.Collider[] GetCollidersInChildren()
			{
				return GetComponentsInChildren<UE.Collider>();
			}

			private void IgnoreSelfCollision()
			{
				if (RootModel == null)
				{
					return;
				}

				var otherLinkPlugins = RootModel.GetComponentsInChildren<Link>();
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
