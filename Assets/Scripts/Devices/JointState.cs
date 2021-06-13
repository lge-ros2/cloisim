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
		private Dictionary<string, JointControl> jointControlTable = new Dictionary<string, JointControl>();

		private messages.JointStateV jointStates = null;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "JointsState";
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
					var jointControl = new JointControl(childArticulatinoBody);
					jointControlTable.Add(linkName, jointControl);
					return true;
				}
			}

			return false;
		}

		public JointControl GetJointControl(in string targetLinkName)
		{
			return jointControlTable[targetLinkName];
		}
	}
}