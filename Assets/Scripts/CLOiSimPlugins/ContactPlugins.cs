/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using Any = cloisim.msgs.Any;

public class ContactPlugin : CLOiSimPlugin
{
	private SensorDevices.Contact _contact = null;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.CONTACT;
		_contact = gameObject.GetComponent<SensorDevices.Contact>();
	}

	private System.IntPtr _rosNode = System.IntPtr.Zero;
	private System.IntPtr _rosPublisher = System.IntPtr.Zero;

	protected override IEnumerator OnStart()
	{
		// Initialize ROS2 Native Plugin
		cloisim.Native.Ros2NativeWrapper.InitROS2(0, System.IntPtr.Zero);
		var nodeName = "cloisim_contact_" + gameObject.name.Replace(" ", "_");
		_rosNode = cloisim.Native.Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/contact");
		_rosPublisher = cloisim.Native.Ros2NativeWrapper.CreateContactsPublisher(_rosNode, topicName);
		
		_contact.OnContactsDataGenerated += HandleNativeContactsData;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _contact);
		}

		yield return null;
	}

	private unsafe void HandleNativeContactsData(cloisim.msgs.Contacts contactsMessage)
	{
		if (_rosPublisher == System.IntPtr.Zero) return;

		int numContacts = contactsMessage.contact.Count;
		var nativeContacts = new cloisim.Native.ContactStruct[numContacts];
		
		// List of allocated pointers to free later
		var allocsToFree = new System.Collections.Generic.List<System.IntPtr>();

		try
		{
			for (int i = 0; i < numContacts; i++)
			{
				var c = contactsMessage.contact[i];
				nativeContacts[i].collision1 = c.Collision1;
				nativeContacts[i].collision2 = c.Collision2;

				// Positions
				int numPositions = c.Positions.Count;
				nativeContacts[i].positions_length = numPositions;
				if (numPositions > 0)
				{
					int posSize = System.Runtime.InteropServices.Marshal.SizeOf<cloisim.Native.Vector3dStruct>();
					System.IntPtr posPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numPositions * posSize);
					allocsToFree.Add(posPtr);
					for (int j = 0; j < numPositions; j++)
					{
                        var vec = new cloisim.Native.Vector3dStruct { x = c.Positions[j].X, y = c.Positions[j].Y, z = c.Positions[j].Z };
						System.IntPtr currentPtr = new System.IntPtr(posPtr.ToInt64() + j * posSize);
						System.Runtime.InteropServices.Marshal.StructureToPtr(vec, currentPtr, false);
					}
					nativeContacts[i].positions = posPtr;
				}
				else
				{
					nativeContacts[i].positions = System.IntPtr.Zero;
				}

				// Normals
				int numNormals = c.Normals.Count;
				nativeContacts[i].normals_length = numNormals;
				if (numNormals > 0)
				{
					int normSize = System.Runtime.InteropServices.Marshal.SizeOf<cloisim.Native.Vector3dStruct>();
					System.IntPtr normPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numNormals * normSize);
					allocsToFree.Add(normPtr);
					for (int j = 0; j < numNormals; j++)
					{
                        var vec = new cloisim.Native.Vector3dStruct { x = c.Normals[j].X, y = c.Normals[j].Y, z = c.Normals[j].Z };
						System.IntPtr currentPtr = new System.IntPtr(normPtr.ToInt64() + j * normSize);
						System.Runtime.InteropServices.Marshal.StructureToPtr(vec, currentPtr, false);
					}
					nativeContacts[i].normals = normPtr;
				}
				else
				{
					nativeContacts[i].normals = System.IntPtr.Zero;
				}

				// Depths
				int numDepths = c.Depths.Length;
				nativeContacts[i].depths_length = numDepths;
				if (numDepths > 0)
				{
					System.IntPtr depthPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numDepths * sizeof(double));
					allocsToFree.Add(depthPtr);
					System.Runtime.InteropServices.Marshal.Copy(c.Depths, 0, depthPtr, numDepths);
					nativeContacts[i].depths = depthPtr;
				}
				else
				{
					nativeContacts[i].depths = System.IntPtr.Zero;
				}
				
				nativeContacts[i].times = System.IntPtr.Zero;
				nativeContacts[i].times_length = 0;
			}

			System.IntPtr contactsPtr = System.IntPtr.Zero;
			if (numContacts > 0)
			{
				int contactSize = System.Runtime.InteropServices.Marshal.SizeOf<cloisim.Native.ContactStruct>();
				contactsPtr = System.Runtime.InteropServices.Marshal.AllocHGlobal(numContacts * contactSize);
				allocsToFree.Add(contactsPtr);
				for (int i = 0; i < numContacts; i++)
				{
					System.IntPtr currentPtr = new System.IntPtr(contactsPtr.ToInt64() + i * contactSize);
					System.Runtime.InteropServices.Marshal.StructureToPtr(nativeContacts[i], currentPtr, false);
				}
			}

			var contactsData = new cloisim.Native.ContactsStruct
			{
				timestamp = contactsMessage.Time.Sec + (contactsMessage.Time.Nsec * 1e-9),
				frame_id = _contact.DeviceName,
				contacts = contactsPtr,
				contacts_length = numContacts
			};

			cloisim.Native.Ros2NativeWrapper.PublishContacts(_rosPublisher, ref contactsData);

			// Destroy structures individually to avoid leaks inside arrays
			if (numContacts > 0)
			{
				int contactSize = System.Runtime.InteropServices.Marshal.SizeOf<cloisim.Native.ContactStruct>();
				for (int i = 0; i < numContacts; i++)
				{
					System.IntPtr currentPtr = new System.IntPtr(contactsPtr.ToInt64() + i * contactSize);
					System.Runtime.InteropServices.Marshal.DestroyStructure<cloisim.Native.ContactStruct>(currentPtr);
				}
			}
		}
		finally
		{
			// Free all allocated memory blocks
			foreach (var ptr in allocsToFree)
			{
				System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr);
			}
		}
	}

	protected void OnDestroy()
	{
		if (_contact != null) _contact.OnContactsDataGenerated -= HandleNativeContactsData;
		if (_rosPublisher != System.IntPtr.Zero) cloisim.Native.Ros2NativeWrapper.DestroyContactsPublisher(_rosPublisher);
		if (_rosNode != System.IntPtr.Zero) cloisim.Native.Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _contact.GetPose();
				var deviceName = _contact.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}
}