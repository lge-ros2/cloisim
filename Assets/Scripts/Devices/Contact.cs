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
		private messages.Contacts _contacts = null;

		public string targetCollision = string.Empty;
		public string topic = string.Empty;

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			DeviceName = name;
		}

		protected override void OnStart()
		{
			// Debug.Log("Contact target collision: " + targetCollision);
		}

		protected override void InitializeMessages()
		{
			_contacts = new messages.Contacts();
			_contacts.Time = new messages.Time();
		}

		protected override void GenerateMessage()
		{
			_contacts.Time.SetCurrentTime();
			PushDeviceMessage<messages.Contacts>(_contacts);
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
			// Debug.Log("CollisionStay: " + other.contacts.Length);
			for (var i = 0; i < other.contacts.Length; i++)
			{
				var collisionContact = other.contacts[i];
				var collision1 = GetColliderName(collisionContact.thisCollider);
				var collision2 = GetColliderName(collisionContact.otherCollider);
				// Debug.Log($"CollisionStay: {collision1} <-> {collision2}");

				if (collision1.EndsWith("::" + targetCollision))
				{
					// find existing collision set
					var existingContact = _contacts.contact.Find(x => x.Collision1.Contains(collision1) && x.Collision2.Contains(collision2));
					if (existingContact == null)
					{
						var newContact = new messages.Contact();
						// newContact.Wrenchs // TODO: Need to be implemented;
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

						newContact.Time.SetCurrentTime();
						// Debug.Log("CollisionStay: " + collision1 + " <-> " + collision2);

						_contacts.contact.Add(newContact);
					}
				}
				// Debug.DrawLine(collisionContact.point, collisionContact.normal, Color.white);
			}

			// Debug.Log("CollisionStay: " + contacts.contact.Count);
		}

		public void CollisionExit(Collision other)
		{
			// Debug.Log($"CollisionExit: {other.contacts.Length}");
			_contacts.contact.Clear();
		}

		public bool IsContacted()
		{
			return (_contacts.contact.Count == 0) ? false : true;
		}
	}
}