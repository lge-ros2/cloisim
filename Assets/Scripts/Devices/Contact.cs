/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class ContactTrigger : MonoBehaviour
	{
		public Action<Collision> collisionStay = null;
		public Action<Collision> collisionExit = null;

		private void OnCollisionStay(Collision other)
		{
			collisionStay?.Invoke(other);
		}

		private void OnCollisionExit(Collision other)
		{
			collisionExit?.Invoke(other);
		}
	}

	public class Contact : Device
	{
		private messages.Contacts _lastContacts = null;

		private double _lastTimeContactsMessageGenerated = 0;

		private string _targetCollision = string.Empty;
		private string _topic = string.Empty;

		public string TargetCollision
		{
			get => _targetCollision;
			set => _targetCollision = value;
		}

		public string Topic
		{
			get => _topic;
			set => _topic = value;
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
		}

		private messages.JointWrench MakeJointWrenchMessage()
		{
			var jointWrench = new messages.JointWrench
			{
				Body1Wrench = new messages.Wrench
				{
					Force = new messages.Vector3d(),
					Torque = new messages.Vector3d()
				},
				Body2Wrench = new messages.Wrench
				{
					Force = new messages.Vector3d(),
					Torque = new messages.Vector3d()
				}
			};
			return jointWrench;
		}

		public string GetColliderName(in Collider collider)
		{
			var collisionHelper = collider.transform.gameObject.GetComponentInParent<SDFormat.Helper.Collision>();
			if (collisionHelper == null)
			{
				return collider.transform.parent.name + "::" + collider.name;
			}
			else
			{
				var linkHelper = collisionHelper.GetComponentInParent<SDFormat.Helper.Link>();
 				return linkHelper.Model.name + "::" + linkHelper.name + "::" + collisionHelper.name;
			}
		}

		public void CollisionStay(Collision other)
		{
			if (Time.timeAsDouble - _lastTimeContactsMessageGenerated < UpdatePeriod)
			{
				return;
			}

			var contactsMessage = new messages.Contacts
			{
				// Debug.Log("{DeviceName} CollisionStay: " + other.contacts.Length);

				Header = new messages.Header
				{
					Stamp = new messages.Time()
				}
			};
			contactsMessage.Header.Stamp.SetCurrentTime();

			var targetSuffix = "::" + _targetCollision;

			for (var i = 0; i < other.contacts.Length; i++)
			{
				var collisionContact = other.contacts[i];
				var collision1 = GetColliderName(collisionContact.thisCollider);
				var collision2 = GetColliderName(collisionContact.otherCollider);
				// Debug.Log($"{DeviceName} CollisionStay: {collision1} <-> {collision2}");

				if (collision1.EndsWith(targetSuffix))
				{
					// find existing collision set
					var existingContact = contactsMessage.contact.Find(x => x.Collision1.Name.Contains(collision1, StringComparison.Ordinal) && x.Collision2.Name.Contains(collision2, StringComparison.Ordinal));
					if (existingContact != null)
					{
						var depths = existingContact.Depths;
						var newLength = depths.Length + 1;
						Array.Resize(ref depths, newLength);
						depths[newLength - 1] = collisionContact.separation;
						existingContact.Depths = depths;

						var normal = new messages.Vector3d();
						normal.Set(collisionContact.normal);
						existingContact.Normals.Add(normal);

						var position = new messages.Vector3d();
						position.Set(collisionContact.point);
						existingContact.Positions.Add(position);

						var jointWrench = MakeJointWrenchMessage();
						jointWrench.Body1Name = collision1;
						jointWrench.Body2Name = collision2;
						jointWrench.Body1Wrench.Force.Set(collisionContact.impulse);
						existingContact.Wrenchs.Add(jointWrench);

						existingContact.Header.Stamp.SetCurrentTime();
					}
					else
					{
						var newContact = new messages.Contact
						{
							World = new messages.Entity { Name = "default" },

							Collision1 = new messages.Entity { Name = collision1 },
							Collision2 = new messages.Entity { Name = collision2 },

							Depths = new double[] { collisionContact.separation }
						};

						var normal = new messages.Vector3d();
						normal.Set(collisionContact.normal);
						newContact.Normals.Add(normal);

						var position = new messages.Vector3d();
						position.Set(collisionContact.point);
						newContact.Positions.Add(position);

						var jointWrench = MakeJointWrenchMessage();
						jointWrench.Body1Name = collision1;
						jointWrench.Body2Name = collision2;
						jointWrench.Body1Wrench.Force.Set(collisionContact.impulse);
						newContact.Wrenchs.Add(jointWrench);

						newContact.Header = new messages.Header
						{
							Stamp = new messages.Time()
						};
						newContact.Header.Stamp.SetCurrentTime();
						// Debug.Log("{DeviceName} CollisionStay: " + collision1 + " <-> " + collision2);

						contactsMessage.contact.Add(newContact);
					}
					// Debug.Log($"{_targetCollision} collisionContact.separation = {collisionContact.separation.ToString("F10")} .impulse = {collisionContact.impulse.ToString("F10")}");
				}
				// Debug.DrawLine(collisionContact.point, collisionContact.normal, Color.white);
			}

			// Debug.Log("{DeviceName} CollisionStay: " + contacts.contact.Count);
			_lastContacts = contactsMessage;

			EnqueueMessage(contactsMessage);
			_lastTimeContactsMessageGenerated = Time.timeAsDouble;
		}


		public void CollisionExit(Collision other)
		{
			_lastContacts = null;
			_lastTimeContactsMessageGenerated = 0;

			var contactsMessage = new messages.Contacts
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				}
			};
			contactsMessage.Header.Stamp.SetCurrentTime();
			EnqueueMessage(contactsMessage);
			// Debug.Log($"{DeviceName} {_targetCollision} CollisionExit: {other.contacts.Length}");
		}

		public bool IsContacted()
		{
			return _lastContacts != null && _lastContacts.contact.Count > 0;
		}

		public messages.Contacts GetContacts()
		{
			return _lastContacts;
		}
	}
}