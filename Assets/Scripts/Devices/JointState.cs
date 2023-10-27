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

		public bool AddTargetJoint(in string targetJointName, out SDF.Helper.Link link, out bool isStatic)
		{
			var childArticulationBodies = gameObject.GetComponentsInChildren<ArticulationBody>();
			var rootModelName = string.Empty;
			link = null;
			isStatic = false;

			foreach (var childArticulationBody in childArticulationBodies)
			{
				// Debug.Log (childArticulationBody.name + " | " + childArticulationBody.transform.parent.name);
				if (childArticulationBody.isRoot)
				{
					rootModelName = childArticulationBody.name;
					continue;
				}

				var parentObject = childArticulationBody.transform.parent;
				var parentModelName = parentObject.name;
				// var linkName = ((!parentObject.CompareTag("Model") || rootModelName.CompareTo(parentModelName) == 0) ? "" : parentModelName + "::") + childArticulationBody.name;
				var linkHelper = childArticulationBody.GetComponentInChildren<SDF.Helper.Link>();
				// Debug.Log("linkHelper.JointName " + linkHelper.JointName);
				if (linkHelper.JointName.Equals(targetJointName))
				{
					// Debug.Log("AddTargetJoint " + targetJointName);
					link = linkHelper;
					if (childArticulationBody.jointType == ArticulationJointType.FixedJoint)
					{
						Debug.LogWarning("Skip to AddTargetJoint due to fixed joint: " + targetJointName);
						isStatic = true;
						return true;
					}

					var articulation = new Articulation(childArticulationBody);

					var jointState = new messages.JointState();
					jointState.Name = targetJointName;

					articulationTable.Add(targetJointName, new Tuple<Articulation, messages.JointState>(articulation, jointState));

					jointStateV.JointStates.Add(jointState);

					// link = articulation.gameObject.GetComponentInChildren<SDF.Helper.Link>();
					return true;
				}
			}

			return false;
		}

		public Articulation GetArticulation(in string targetJointName)
		{
			return articulationTable.ContainsKey(targetJointName) ? articulationTable[targetJointName].Item1 : null;
		}

		void FixedUpdate()
		{
			foreach (var item in articulationTable.Values)
			{
				var articulation = item.Item1;
				var jointState = item.Item2;

				jointState.Effort = articulation.GetEffort();
				jointState.Position = (articulation.IsRevoluteType() ?
					DeviceHelper.Convert.CurveOrientation(articulation.GetJointPosition()) : articulation.GetJointPosition());
				jointState.Velocity = (articulation.IsRevoluteType() ?
					DeviceHelper.Convert.CurveOrientation(articulation.GetJointVelocity()) : articulation.GetJointVelocity());
			}
		}
	}
}