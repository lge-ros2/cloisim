/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
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

		public List<string> collision = new List<string>();
		public string topic = string.Empty;

		private bool contacted = false;

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			deviceName = name;
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
			DeviceHelper.SetCurrentTime(contacts.Time);
			PushDeviceMessage<messages.Contacts>(contacts);
			// if (contacts.contact.Count > 0)
			// {
			// 	Debug.Log(contacts.contact[0].Depths.Length + " : " + contacts.contact[0].Normals.Count);
			// }
			contacts.contact.Clear();
		}

		public void CollisionEnter(Collision other)
		{
			if (other.contactCount > 0)
			{
				contacted = true;
			}
		}

		public void CollisionStay(Collision other)
		{
			var newContact = new messages.Contact();

			// TODO: Need to be implemented;
			// newContact.Wrenchs
			newContact.Depths = new double[1];
			newContact.World = "default";
			DeviceHelper.SetCurrentTime(newContact.Time);

			foreach (var collisionContact in other.contacts)
			{
				var collision1 = collisionContact.thisCollider.name;
				var collision2 = collisionContact.otherCollider.name;

				// find existing collision set
				var existingContact = contacts.contact.Find(x => x.Collision1.Contains(collision1) && x.Collision2.Contains(collision2));
				if (existingContact != null)
				{
					// Debug.Log("Existing!!");
					var depths = existingContact.Depths;
					var depthsLength = depths.Length;
					Array.Resize(ref depths, depthsLength + 1);
					depths[depthsLength] = collisionContact.separation;
					existingContact.Depths = depths;

					var normal = new messages.Vector3d();
					DeviceHelper.SetVector3d(normal, collisionContact.normal);
					existingContact.Normals.Add(normal);

					var position = new messages.Vector3d();
					DeviceHelper.SetVector3d(position, collisionContact.point);
					existingContact.Positions.Add(position);
				}
				else
				{
					newContact.Collision1 = collisionContact.thisCollider.name;
					newContact.Collision2 = collisionContact.otherCollider.name;

					newContact.Depths[0] = collisionContact.separation;

					var normal = new messages.Vector3d();
					DeviceHelper.SetVector3d(normal, collisionContact.normal);
					newContact.Normals.Add(normal);

					var position = new messages.Vector3d();
					DeviceHelper.SetVector3d(position, collisionContact.point);
					newContact.Positions.Add(position);

					contacts.contact.Add(newContact);
				}
				// Debug.DrawLine(collisionContact.point, collisionContact.normal, Color.white);
			}
			// Debug.Log(other.contactCount + "," + contacts.contact.Count);
			// Debug.Log(contacts.contact[0].Depths.Length + " : " + contacts.contact[0].Normals.Count);
		}

		public void CollisionExit(Collision other)
		{
			if (other.contactCount == 0)
			{
				contacted = false;
			}
		}

		public bool IsContacted()
		{
			return contacted;
		}
	}
}