/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
	namespace Helper
	{
		// Rigidly attaches an ArticulationBody island root to a link that lives in a
		// different island, since PhysX offers no supported way to bridge two
		// separate ArticulationBody islands with a real joint. Every FixedUpdate,
		// the root is teleported to the parent link's current pose plus a fixed
		// offset, and its velocity is copied (with a lever-arm correction) so
		// external collisions still see a body moving consistently with its parent.
		// This trades away real force/torque feedback across the connection: a
		// collision on this island will not push back on the parent.
		public class KinematicIslandFollower : UE.MonoBehaviour
		{
			private UE.Transform _parentLink;
			private UE.ArticulationBody _parentArticulationBody;
			private UE.ArticulationBody _articulationBody;
			private UE.Vector3 _localOffsetPosition;
			private UE.Quaternion _localOffsetRotation;

			public void Initialize(in UE.Transform parentLink, in UE.Vector3 localOffsetPosition, in UE.Quaternion localOffsetRotation)
			{
				_parentLink = parentLink;
				_parentArticulationBody = parentLink.GetComponent<UE.ArticulationBody>();
				_articulationBody = GetComponent<UE.ArticulationBody>();
				_localOffsetPosition = localOffsetPosition;
				_localOffsetRotation = localOffsetRotation;
			}

			void FixedUpdate()
			{
				if (_articulationBody == null || _parentLink == null)
				{
					return;
				}

				var worldPosition = _parentLink.TransformPoint(_localOffsetPosition);
				var worldRotation = _parentLink.rotation * _localOffsetRotation;

				_articulationBody.TeleportRoot(worldPosition, worldRotation);

				if (_parentArticulationBody != null)
				{
					var parentLinearVelocity = _parentArticulationBody.linearVelocity;
					var parentAngularVelocity = _parentArticulationBody.angularVelocity;
					var leverArm = worldPosition - _parentLink.position;

					_articulationBody.linearVelocity = parentLinearVelocity + UE.Vector3.Cross(parentAngularVelocity, leverArm);
					_articulationBody.angularVelocity = parentAngularVelocity;
				}
			}
		}
	}
}
