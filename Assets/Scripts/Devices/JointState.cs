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
					articulation.SetVelocityLimit(linkHelper.JointAxisLimitVelocity);

					var jointState = new messages.JointState();
					jointState.Name = targetJointName;

					articulationTable.Add(targetJointName, new Tuple<Articulation, messages.JointState>(articulation, jointState));

					jointStateV.JointStates.Add(jointState);
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

				var jointVelocity = articulation.GetJointVelocity();
				var jointPosition = articulation.GetJointPosition();
				var jointForce = articulation.GetForce();

				jointState.Effort = (articulation.IsRevoluteType()) ?
										DeviceHelper.Convert.CurveOrientation(jointForce) :
											(articulation.IsPrismaticType() ?
												 DeviceHelper.Convert.PrismaticDirection(jointForce, articulation.GetAnchorRotation()) : jointForce);

				jointState.Position = (articulation.IsRevoluteType()) ?
										DeviceHelper.Convert.CurveOrientation(jointPosition) :
											(articulation.IsPrismaticType() ?
												 DeviceHelper.Convert.PrismaticDirection(jointPosition, articulation.GetAnchorRotation()) : jointPosition);

				jointState.Velocity = (articulation.IsRevoluteType()) ?
										DeviceHelper.Convert.CurveOrientation(jointVelocity) : jointVelocity;
			}
		}
	}
}