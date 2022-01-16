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
	public class JointState : Device
	{
		private Dictionary<string, Tuple<Articulation, messages.JointState>> articulationTable = new Dictionary<string, Tuple<Articulation, messages.JointState>>();

		private messages.JointStateV jointStateV = null;

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
			jointStateV = new messages.JointStateV();
			jointStateV.Header = new messages.Header();
			jointStateV.Header.Stamp = new messages.Time();
		}

		protected override void GenerateMessage()
		{
			DeviceHelper.SetCurrentTime(jointStateV.Header.Stamp);
			PushDeviceMessage<messages.JointStateV>(jointStateV);
		}

		public bool AddTarget(in string targetLinkName, out SDF.Helper.Link link)
		{
			var childArticulationBodies = gameObject.GetComponentsInChildren<ArticulationBody>();
			var rootModelName = string.Empty;
			foreach (var childArticulationBody in childArticulationBodies)
			{
				// Debug.Log (childArticulationBody.name + " | " + childArticulationBody.transform.parent.name);
				if (childArticulationBody.isRoot)
				{
					rootModelName = childArticulationBody.name;
					continue;
				}

				var parentModelName = childArticulationBody.transform.parent.name;
				var linkName = ((rootModelName.CompareTo(parentModelName) == 0) ? "" : parentModelName + "::") + childArticulationBody.name;
				// Debug.Log("!!!!!!! " + linkName);
				if (linkName.Equals(targetLinkName))
				{
					var articulation = new Articulation(childArticulationBody);
					articulation.SetDriveType(Articulation.DriveType.POSITION_AND_VELOCITY);

					var jointState = new messages.JointState();
					jointState.Name = targetLinkName;

					articulationTable.Add(targetLinkName, new Tuple<Articulation, messages.JointState>(articulation, jointState));

					jointStateV.JointStates.Add(jointState);

					link = articulation.gameObject.GetComponentInChildren<SDF.Helper.Link>();
					return true;
				}
			}

			link = null;
			return false;
		}

		public Articulation GetArticulation(in string targetLinkName)
		{
			return articulationTable.ContainsKey(targetLinkName) ? articulationTable[targetLinkName].Item1 : null;
		}

		void FixedUpdate()
		{
			foreach (var item in articulationTable.Values)
			{
				var articulation = item.Item1;
				var jointState = item.Item2;

				jointState.Effort = articulation.GetEffort();
				jointState.Position = articulation.GetJointPosition();
				jointState.Velocity = articulation.GetJointVelocity();
			}
		}
	}
}