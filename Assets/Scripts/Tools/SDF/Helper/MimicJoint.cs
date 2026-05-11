/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDFormat
{
	namespace Helper
	{
		public class MimicJoint : UE.MonoBehaviour
		{
			private string _leaderJointName = string.Empty;
			private int _leaderPositionIndex = 0;

			private float _multiplier = 1f;
			private float _offset = 0f;
			private float _reference = 0f;

			private bool _isRevoluteType = false;

			private UE.ArticulationBody _leaderBody = null;
			private UE.ArticulationBody _followerBody = null;
			private UE.ArticulationDriveAxis _followerDriveAxis;

			public void Initialize(
				in MimicConstraint mimic,
				in JointType jointType,
				in UE.ArticulationBody followerBody)
			{
				_leaderJointName = mimic.Joint;
				_leaderPositionIndex = mimic.Axis.Equals("axis2") ? 1 : 0;

				_isRevoluteType = (jointType != JointType.Prismatic);

				if (_isRevoluteType)
				{
					// Convert SDF radians → Unity radians (negate for handedness)
					_multiplier = (float)mimic.Multiplier;
					_offset = SDF2Unity.CurveOrientationAngle((float)mimic.Offset);
					_reference = SDF2Unity.CurveOrientationAngle((float)mimic.Reference);
				}
				else
				{
					_multiplier = (float)mimic.Multiplier;
					_offset = (float)mimic.Offset;
					_reference = (float)mimic.Reference;
				}

				_followerBody = followerBody;
			}

			void Start()
			{
				if (_followerBody == null)
				{
					Debug.LogWarning($"[MimicJoint] Follower ArticulationBody is null on '{name}', disabling.");
					enabled = false;
					return;
				}

				_followerDriveAxis = GetDriveAxis(_followerBody);

				_leaderBody = FindLeaderBody();
				if (_leaderBody == null)
				{
					Debug.LogWarning($"[MimicJoint] Leader joint '{_leaderJointName}' not found for follower '{name}', disabling.");
					enabled = false;
					return;
				}
			}

			void FixedUpdate()
			{
				if (_leaderBody == null || _followerBody == null)
				{
					return;
				}

				var leaderPosition = _leaderBody.jointPosition[_leaderPositionIndex];

				// follower_position = multiplier * (leader_position - reference) + offset
				var targetRad = _multiplier * (leaderPosition - _reference) + _offset;

				if (_isRevoluteType)
				{
					_followerBody.SetDriveTarget(_followerDriveAxis, targetRad * UE.Mathf.Rad2Deg);
				}
				else
				{
					_followerBody.SetDriveTarget(_followerDriveAxis, targetRad);
				}
			}

			private UE.ArticulationBody FindLeaderBody()
			{
				// Search from root model scope to find the leader joint by name
				var linkHelper = GetComponent<Link>();
				if (linkHelper == null)
				{
					return null;
				}

				var searchRoot = (linkHelper.RootModel != null)
					? linkHelper.RootModel.transform
					: transform.root;

				var linkHelpers = searchRoot.GetComponentsInChildren<Link>();
				foreach (var lh in linkHelpers)
				{
					if (lh.JointName.Equals(_leaderJointName))
					{
						return lh.GetComponent<UE.ArticulationBody>();
					}
				}

				return null;
			}

			private static UE.ArticulationDriveAxis GetDriveAxis(UE.ArticulationBody body)
			{
				switch (body.jointType)
				{
					case UE.ArticulationJointType.RevoluteJoint:
						return UE.ArticulationDriveAxis.X;

					case UE.ArticulationJointType.PrismaticJoint:
						if (body.linearLockX != UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.X;
						if (body.linearLockY != UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.Y;
						if (body.linearLockZ != UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.Z;
						break;

					case UE.ArticulationJointType.SphericalJoint:
						if (body.twistLock != UE.ArticulationDofLock.LockedMotion &&
							body.swingYLock == UE.ArticulationDofLock.LockedMotion &&
							body.swingZLock == UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.X;
						if (body.twistLock == UE.ArticulationDofLock.LockedMotion &&
							body.swingYLock != UE.ArticulationDofLock.LockedMotion &&
							body.swingZLock == UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.Y;
						if (body.twistLock == UE.ArticulationDofLock.LockedMotion &&
							body.swingYLock == UE.ArticulationDofLock.LockedMotion &&
							body.swingZLock != UE.ArticulationDofLock.LockedMotion)
							return UE.ArticulationDriveAxis.Z;
						break;
				}

				Debug.LogWarning($"[MimicJoint] Cannot determine drive axis for '{body.name}', defaulting to X.");
				return UE.ArticulationDriveAxis.X;
			}
		}
	}
}
