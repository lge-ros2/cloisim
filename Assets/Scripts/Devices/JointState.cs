/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class JointState : Device
	{
		private Dictionary<string, Articulation> articulationTable = new Dictionary<string, Articulation>();

		private messages.JointStateV jointStates = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "JointState";
		}

		protected override void OnStart()
		{
		}

		protected override void OnReset()
		{
		}

		protected override void InitializeMessages()
		{
			jointStates = new messages.JointStateV();
			jointStates.Header = new messages.Header();
			jointStates.Header.Stamp = new messages.Time();
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(jointStates.Header.Stamp);


			PushDeviceMessage<messages.JointStateV>(jointStates);
		}

		public bool AddTarget(in string linkName)
		{
			var childArticulationBodies = gameObject.GetComponentsInChildren<ArticulationBody>();

			foreach (var childArticulatinoBody in childArticulationBodies)
			{
				if (childArticulatinoBody.name.Equals(linkName))
				{
					var articulation = new Articulation(childArticulatinoBody);
					articulationTable.Add(linkName, articulation);
					return true;
				}
			}

			return false;
		}

		public Articulation GetArticulation(in string targetLinkName)
		{
			return articulationTable.ContainsKey(targetLinkName) ? articulationTable[targetLinkName] : null;
		}
	}
}