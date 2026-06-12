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

			public bool isRevolute;
			public bool isPrismatic;
			public Vector3 anchorRotation;
		}

		private struct InterpolationData
		{
			public messages.JointState message;
			public PhysicsSample prev;
			public PhysicsSample curr;
		}

		private Dictionary<string, JointEntry> articulationTable = new();
		private List<InterpolationData> _interpolationBuffer = new();

		private messages.JointStateV jointStateV = null;

		private readonly object _jointStateLock = new();

		// Physics timing for interpolation
		private double _lastFixedSimTime = 0;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = "JointState";
		}

		protected override void InitializeMessages()
		{
			jointStateV = new messages.JointStateV
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				}
			};
		}

		protected override void GenerateMessage()
		{
			var t = 0.0;

			lock (_jointStateLock)
			{
				if (_lastFixedSimTime <= 0)
					return; // No physics data yet

				// Compute interpolation fraction: how far past the last FixedUpdate are we?
				// SimTime advances with render frames while FixedSimTime advances with physics.
				var fixedSimTime = (Clock != null) ? Clock.FixedSimTime : (double)Time.fixedTimeAsDouble;
				var fixedDeltaTime = Clock.FixedDeltaTime;
				if (fixedDeltaTime > 0)
				{
					t = (fixedSimTime - _lastFixedSimTime) / fixedDeltaTime;
					t = Math.Max(0.0, Math.Min(1.0, t));
				}

				// Copy data to buffer under lock to minimize lock duration
				_interpolationBuffer.Clear();
				foreach (var entry in articulationTable.Values)
				{
					_interpolationBuffer.Add(new InterpolationData
					{
						message = entry.message,
						prev = entry.prev,
						curr = entry.curr
					});
				}

				// Fixed-dt synthetic timestamp: advances by exactly UpdatePeriod (1ms at 1000 Hz)
				// per publish for jitter-free timestamps.
				jointStateV.Header.Stamp.Set(GetNextSyntheticTime());
			}

			// Perform interpolation outside the lock
			for (var i = 0; i < _interpolationBuffer.Count; i++)
			{
				var data = _interpolationBuffer[i];
				data.message.Position = data.prev.position + (data.curr.position - data.prev.position) * t;
				data.message.Velocity = data.prev.velocity + (data.curr.velocity - data.prev.velocity) * t;
				data.message.Effort = data.prev.effort + (data.curr.effort - data.prev.effort) * t;
			}

			PushDeviceMessage(jointStateV);

#if UNITY_EDITOR
			UpdateProfiler("JOINTSTATE", jointStateV.JointStates.Count * sizeof(double) * 3);
#endif
		}

		public bool AddTargetJoint(in string targetJointName, out SDFormat.Helper.Link link, out bool isStatic)
		{
			lock (_jointStateLock)
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

					var linkHelper = childArticulationBody.GetComponentInChildren<SDFormat.Helper.Link>();
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

						var jointState = new messages.JointState
						{
							Name = targetJointName
						};

						var entry = new JointEntry
						{
							articulation = articulation,
							message = jointState,
							isRevolute = articulation.IsRevoluteType(),
							isPrismatic = articulation.IsPrismaticType(),
							anchorRotation = articulation.GetAnchorRotation()
						};

						articulationTable.Add(targetJointName, entry);

						jointStateV.JointStates.Add(jointState);
						return true;
					}
				}

				return false;
			}
		}

		public Articulation GetArticulation(in string targetJointName)
		{
			return articulationTable.ContainsKey(targetJointName) ? articulationTable[targetJointName].articulation : null;
		}

		void FixedUpdate()
		{
			lock (_jointStateLock)
			{
				// Record physics timing (once per FixedUpdate, not per joint)
				var fixedSimTime = (Clock != null) ? Clock.FixedSimTime : (double)Time.fixedTimeAsDouble;
				_lastFixedSimTime = fixedSimTime;

				foreach (var entry in articulationTable.Values)
				{
					var articulation = entry.articulation;

					// Shift current → previous for interpolation
					entry.prev = entry.curr;

					var jointVelocity = articulation.GetJointVelocity();
					var jointPosition = articulation.GetJointPosition();
					var jointForce = articulation.GetForce();

					var effort = entry.isRevolute ?
									Unity2SDF.Direction.Curve(jointForce) :
										(entry.isPrismatic ?
											 Unity2SDF.Direction.Joint.Prismatic(jointForce, entry.anchorRotation) : jointForce);

					var position = entry.isRevolute ?
									Unity2SDF.Direction.Curve(jointPosition) :
										(entry.isPrismatic ?
											 Unity2SDF.Direction.Joint.Prismatic(jointPosition, entry.anchorRotation) : jointPosition);

					var velocity = entry.isRevolute ?
									Unity2SDF.Direction.Curve(jointVelocity) : jointVelocity;

					entry.curr = new PhysicsSample
					{
						position = position,
						velocity = velocity,
						effort = effort
					};
				}
				;
			}
		}
	}
}