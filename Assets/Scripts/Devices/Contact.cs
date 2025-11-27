/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Concurrent;
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
			if (collisionStay != null)
			{
				collisionStay.Invoke(other);
			}
		}

		private void OnCollisionExit(Collision other)
		{
			if (collisionExit != null)
			{
				collisionExit.Invoke(other);
			}
		}
	}

	public class Contact : Device
	{
		private ConcurrentQueue<messages.Contacts> _messageQueue = new();

		private messages.Contacts _lastContacts = null;

		private double _lastTimeContactsMessageGenerated = 0;

		public string targetCollision = string.Empty;
		public string topic = string.Empty;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
		}

		protected override void OnStart()
		{
			// Debug.Log("Contact target collision: " + targetCollision);
		}

		protected override void OnReset()
		{
			_messageQueue.Clear();
		}

		protected override void InitializeMessages()
		{
		}

		protected override void GenerateMessage()
		{
			if (_messageQueue.Count == 0)
			{
				var contactsMessage = new messages.Contacts();
				contactsMessage.Time = new messages.Time();
				contactsMessage.Time.SetCurrentTime();
				_messageQueue.Enqueue(contactsMessage);
			}

			while (_messageQueue.TryDequeue(out var msg))
			{
				PushDeviceMessage<messages.Contacts>(msg);
			}
		}

		private messages.JointWrench MakeJointWrenchMessage()
		{
			var jointWrench = new messages.JointWrench();
			jointWrench.Body1Wrench = new messages.Wrench();
			jointWrench.Body1Wrench.Force = new messages.Vector3d();
			jointWrench.Body1Wrench.Torque = new messages.Vector3d();
			jointWrench.Body2Wrench = new messages.Wrench();
			jointWrench.Body2Wrench.Force = new messages.Vector3d();
			jointWrench.Body2Wrench.Torque = new messages.Vector3d();
			return jointWrench;
		}

		public string GetColliderName(in Collider collider)
		{
			var collisionHelper = collider.transform.gameObject.GetComponentInParent<SDF.Helper.Collision>();
			if (collisionHelper == null)
			{
				return collider.transform.parent.name + "::" + collider.name;
			}
			else
			{
				var linkHelper = collisionHelper.GetComponentInParent<SDF.Helper.Link>();
 				return linkHelper.Model.name + "::" + linkHelper.name + "::" + collisionHelper.name;
			}
		}

		public void CollisionEnter(Collision other)
		{
			// Debug.Log("CollisionEnter: " + other.contacts.Length);
		}

		public void CollisionStay(Collision other)
		{
			if (Time.timeAsDouble - _lastTimeContactsMessageGenerated < UpdatePeriod)
			{
				return;
			}

			var contactsMessage = new messages.Contacts();
			// Debug.Log("CollisionStay: " + other.contacts.Length);

			contactsMessage.Time = new messages.Time();
			contactsMessage.Time.SetCurrentTime();
			contactsMessage.contact.Clear();

			for (var i = 0; i < other.contacts.Length; i++)
			{
				var collisionContact = other.contacts[i];
				var collision1 = GetColliderName(collisionContact.thisCollider);
				var collision2 = GetColliderName(collisionContact.otherCollider);
				// Debug.Log($"CollisionStay: {collision1} <-> {collision2}");

				if (collision1.EndsWith("::" + targetCollision))
				{
					// find existing collision set
					var existingContact = contactsMessage.contact.Find(x => x.Collision1.Contains(collision1) && x.Collision2.Contains(collision2));
					if (existingContact != null)
					{
						var depth = new double[existingContact.Depths.Length + 1];
						Array.Copy(existingContact.Depths, depth, existingContact.Depths.Length);
						depth[existingContact.Depths.Length] = collisionContact.separation;

						existingContact.Depths = depth;

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

						existingContact.Time.SetCurrentTime();
					}
					else
					{
						var newContact = new messages.Contact();
						newContact.World = "default";

						newContact.Collision1 = collision1;
						newContact.Collision2 = collision2;

						newContact.Depths = new double[] { collisionContact.separation };

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

						newContact.Time = new messages.Time();
						newContact.Time.SetCurrentTime();
						// Debug.Log("CollisionStay: " + collision1 + " <-> " + collision2);

						contactsMessage.contact.Add(newContact);
					}
				}
				// Debug.DrawLine(collisionContact.point, collisionContact.normal, Color.white);
			}

			// Debug.Log("CollisionStay: " + contacts.contact.Count);
			_lastContacts = contactsMessage;

			_messageQueue.Enqueue(contactsMessage);
			_lastTimeContactsMessageGenerated = Time.timeAsDouble;
		}


		public void CollisionExit(Collision other)
		{
			_lastContacts = null;
			// Debug.Log($"CollisionExit: {other.contacts.Length}");
			_lastTimeContactsMessageGenerated = 0;
		}

		public bool IsContacted()
		{
			return (_lastContacts != null && _lastContacts.contact.Count > 0) ? true : false;
		}

		public messages.Contacts GetContacts()
		{
			return _lastContacts;
		}
	}
}