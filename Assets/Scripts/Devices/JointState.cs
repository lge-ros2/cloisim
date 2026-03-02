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
		/// <summary>
		/// Per-joint interpolation state.
		/// Physics FixedUpdate runs at ~100 Hz; JointState publishes at up to 1000 Hz.
		/// We store two consecutive physics frames and interpolate between them so that
		/// each TX message contains smooth intermediate values rather than stale repeats.
		/// </summary>
		private struct PhysicsSample
		{
			public double position;
			public double velocity;
			public double effort;
		}

		private class JointEntry
		{
			public Articulation articulation;
			public messages.JointState message;

			// Double-buffered physics samples for interpolation
			public PhysicsSample prev;
			public PhysicsSample curr;
		}

		private Dictionary<string, JointEntry> articulationTable = new Dictionary<string, JointEntry>();

		private messages.JointStateV jointStateV = null;

		private readonly object _jointStateLock = new object();

		// Physics timing for interpolation
		private double _prevFixedSimTime = 0;
		private double _currFixedSimTime = 0;
		private double _fixedDeltaTime = 0;

		private Clock _clock = null;

		public System.Action<messages.JointStateV> OnJointStateDataGenerated;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "JointState";
		}

		protected override void OnStart()
		{
			_clock = DeviceHelper.GetGlobalClock();
		}

		protected override void InitializeMessages()
		{
			jointStateV = new messages.JointStateV();
			jointStateV.Header = new messages.Header();
			jointStateV.Header.Stamp = new messages.Time();
		}

		protected override void GenerateMessage()
		{
			lock (_jointStateLock)
			{
				if (_currFixedSimTime <= 0)
					return; // No physics data yet

				// Compute interpolation fraction: how far past the last FixedUpdate are we?
				// SimTime advances with render frames while FixedSimTime advances with physics.
				var simTime = (_clock != null) ? _clock.SimTime : (double)Time.timeAsDouble;
				var t = 0.0;
				if (_fixedDeltaTime > 0)
				{
					t = (simTime - _currFixedSimTime) / _fixedDeltaTime;
					t = Math.Max(0.0, Math.Min(1.0, t));
				}

				// Interpolate each joint between prev and curr physics samples
				foreach (var entry in articulationTable.Values)
				{
					entry.message.Position = entry.prev.position + (entry.curr.position - entry.prev.position) * t;
					entry.message.Velocity = entry.prev.velocity + (entry.curr.velocity - entry.prev.velocity) * t;
					entry.message.Effort = entry.prev.effort + (entry.curr.effort - entry.prev.effort) * t;
				}

				// Fixed-dt synthetic timestamp: advances by exactly UpdatePeriod (1ms at 1000 Hz)
				// per publish for jitter-free timestamps.
				jointStateV.Header.Stamp.Set(GetNextSyntheticTime());

				OnJointStateDataGenerated?.Invoke(jointStateV);

				PushDeviceMessage<messages.JointStateV>(jointStateV);
			}
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

					var entry = new JointEntry
					{
						articulation = articulation,
						message = jointState
					};

					articulationTable.Add(targetJointName, entry);

					lock (_jointStateLock)
					{
						jointStateV.JointStates.Add(jointState);
					}
					return true;
				}
			}

			return false;
		}

		public Articulation GetArticulation(in string targetJointName)
		{
			return articulationTable.ContainsKey(targetJointName) ? articulationTable[targetJointName].articulation : null;
		}

		void FixedUpdate()
		{
			lock (_jointStateLock)
			{
				// Record physics timing
				var fixedSimTime = (_clock != null) ? _clock.FixedSimTime : (double)Time.fixedTimeAsDouble;
				_fixedDeltaTime = (double)Time.fixedDeltaTime;
				_prevFixedSimTime = _currFixedSimTime;
				_currFixedSimTime = fixedSimTime;

				foreach (var entry in articulationTable.Values)
				{
					var articulation = entry.articulation;

					// Shift current → previous for interpolation
					entry.prev = entry.curr;

					var jointVelocity = articulation.GetJointVelocity();
					var jointPosition = articulation.GetJointPosition();
					var jointForce = articulation.GetForce();

					var effort = (articulation.IsRevoluteType()) ?
									Unity2SDF.Direction.Curve(jointForce) :
										(articulation.IsPrismaticType() ?
											 Unity2SDF.Direction.Joint.Prismatic(jointForce, articulation.GetAnchorRotation()) : jointForce);

					var position = (articulation.IsRevoluteType()) ?
									Unity2SDF.Direction.Curve(jointPosition) :
										(articulation.IsPrismaticType() ?
											 Unity2SDF.Direction.Joint.Prismatic(jointPosition, articulation.GetAnchorRotation()) : jointPosition);

					var velocity = (articulation.IsRevoluteType()) ?
									Unity2SDF.Direction.Curve(jointVelocity) : jointVelocity;

					entry.curr = new PhysicsSample
					{
						position = position,
						velocity = velocity,
						effort = effort
					};
				}
			}
		}
	}
}