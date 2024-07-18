/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class ContactTrigger : MonoBehaviour
	{
		public Action<Collision> collisionEnter = null;
		public Action<Collision> collisionStay = null;
		public Action<Collision> collisionExit = null;

		private void OnCollisionEnter(Collision other)
		{
			if (collisionEnter != null)
			{
				collisionEnter.Invoke(other);
			}
		}

		private void OnCollisionExit(Collision other)
		{
			if (collisionExit != null)
			{
				collisionExit.Invoke(other);
			}
		}

		private void OnCollisionStay(Collision other)
		{
			if (collisionStay != null)
			{
				collisionStay.Invoke(other);
			}
		}
	}

	public class Contact : Device
	{
		private messages.Contacts contacts = null;

		public string targetCollision = string.Empty;
		public string topic = string.Empty;

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			DeviceName = name;
		}

		protected override void OnStart()
		{
		}

		protected override void InitializeMessages()
		{
			contacts = new messages.Contacts();
			contacts.Time = new messages.Time();
		}

		protected override void GenerateMessage()
		{
			contacts.Time.SetCurrentTime();
			// if (contacts.contact.Count > 0)
			// {
			// 	Debug.Log(contacts.contact[0].Depths.Length + " : " + contacts.contact[0].Normals.Count);
			// }
			PushDeviceMessage<messages.Contacts>(contacts);
		}

		public string GetColliderParentName(in Collider collider)
		{
			return collider.transform.parent.name;
		}

		public string GetColliderName(in Collider collider)
		{
			var childName = collider.name;
			var parentName = GetColliderParentName(collider);
			return parentName + "::" + childName;
		}

		public void CollisionEnter(Collision other)
		{
			for (var i = 0; i < other.contacts.Length; i++)
			{
				var collisionContact = other.contacts[i];
				var collision1 = GetColliderParentName(collisionContact.thisCollider);
				var collision2 = GetColliderName(collisionContact.otherCollider);
				// Debug.Log("CollisionEnter: " + collision1 + " <-> " + collision2);

				if (string.Equals(collision1, targetCollision))
				{
					// find existing collision set
					var foundContact = contacts.contact.Find(x => x.Collision1.Contains(collision1) && x.Collision2.Contains(collision2));
					if (foundContact == null)
					{
						var newContact = new messages.Contact();
						// newContact.Wrenchs // TODO: Need to be implemented;
						newContact.World = "default";

						newContact.Collision1 = collision1;
						newContact.Collision2 = collision2;

						newContact.Depths = new double[0];

						contacts.contact.Add(newContact);
					}
				}
			}
		}

		public void CollisionStay(Collision other)
		{
			for (var i = 0; i < other.contacts.Length; i++)
			{
				var collisionContact = other.contacts[i];
				var collision1 = GetColliderParentName(collisionContact.thisCollider);
				if (!string.Equals(collision1, targetCollision))
				{
					continue;
				}

				var collision2 = GetColliderName(collisionContact.otherCollider);
				// Debug.Log("CollsiionStay: " + collision1 + " " + collision2);

				// find existing collision set
				var existingContact = contacts.contact.Find(x => x.Collision1.Contains(collision1) && x.Collision2.Contains(collision2));
				if (existingContact != null)
				{
					// Debug.Log("Existing!!");
					var depths = existingContact.Depths;
					Array.Resize(ref depths, depths.Length + 1);
					depths[depths.Length - 1] = collisionContact.separation;
					existingContact.Depths = depths;

					var normal = new messages.Vector3d();
					DeviceHelper.SetVector3d(normal, collisionContact.normal);
					existingContact.Normals.Add(normal);

					var position = new messages.Vector3d();
					DeviceHelper.SetVector3d(position, collisionContact.point);
					existingContact.Positions.Add(position);

					existingContact.Time.SetCurrentTime();
					// Debug.Log("CollisionStay: " + collision1 + " <-> " + collision2);
				}

				// Debug.DrawLine(collisionContact.point, collisionContact.normal, Color.white);
			}
		}

		public void CollisionExit(Collision other)
		{
			// Debug.Log("CollisionExit: " + other.contactCount + " ," + other.collider.name);
			var collision2 = GetColliderName(other.collider);
			var foundContacts = contacts.contact.FindAll(x => x.Collision2.Contains(collision2));

			foreach (var foundContact in foundContacts)
			{
				// Debug.Log("CollisionExit: Remove " + foundContact.Collision1 + " <-> " + foundContact.Collision2);
				contacts.contact.Remove(foundContact);
			}

			// if (contacts.contact.Count == 0)
			// {
			// 	Debug.Log("CollisionExit: no contacts");
			// }
		}

		public bool IsContacted()
		{
			return (contacts.contact.Count == 0) ? false : true;
		}
	}
}