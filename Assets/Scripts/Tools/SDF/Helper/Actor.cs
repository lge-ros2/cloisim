/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Actor : Base
		{
			private struct waypointToward
			{
				public float linearSpeed;
				public float angularSpeed;
				public UE.Vector3 tranlateTo;
				public UE.Quaternion rotateTo;
			};

			private const float distanceEpsilon = UE.Vector3.kEpsilon * 10;
			private const float angleEpsilon = UE.Quaternion.kEpsilon * 50000;

			private List<waypointToward> waypointTowards = new List<waypointToward>();
			private int waypointTowardsIndex = 0;
			private double elapsedTimeSinceAnimationStarted = 0;
			private bool _followingWaypoint = false;
			private UE.Pose trgetPose = new UE.Pose();

			[UE.Header("SDF Properties")]
			public bool isStatic = false;
			private SDF.Actor.Script _script = null;

			public bool IsFollowingWaypoint => _followingWaypoint;

			new void Awake()
			{
				base.Awake();
			}

			public new void Reset()
			{
				base.Reset();

				var animationComponent = GetComponent<UE.Animation>();
				animationComponent.Rewind();

				RestartWayPointFollowing();
			}

			void Start()
			{
				if (_script != null && _script.auto_start && _script.trajectories.Count > 0)
				{
					StartWaypointFollowing();
				}
			}

			void LateUpdate()
			{
				if (_followingWaypoint && waypointTowardsIndex < waypointTowards.Count)
				{
					if (elapsedTimeSinceAnimationStarted < _script.delay_start)
					{
						elapsedTimeSinceAnimationStarted += UE.Time.timeAsDouble;
						// UE.Debug.Log("waiting for start: " + elapsedTimeSinceAnimationStarted);
						return;
					}

					var deltaTime = UE.Time.deltaTime;
					var waypoint = waypointTowards[waypointTowardsIndex];
					var linearSpeed = waypoint.linearSpeed;
					var angularSpeed = waypoint.angularSpeed;
					var moveTo = waypoint.tranlateTo;
					var rotateTo = waypoint.rotateTo;

					var currentPose = CurrentActorPose();
					var nextPosition = UE.Vector3.MoveTowards(currentPose.position, moveTo, linearSpeed * deltaTime);
					var nextRotation = UE.Quaternion.RotateTowards(currentPose.rotation, rotateTo, angularSpeed * deltaTime);
					var nextPose = SetActorPose(nextPosition, nextRotation);

					var diffPos = UE.Vector3.Distance(nextPose.position, moveTo);
					var diffRot = UE.Quaternion.Angle(nextPose.rotation, rotateTo);
					// UE.Debug.Log("pos diff:" + diffPos + ", rot diff: " + diffRot);
					if ((diffPos < distanceEpsilon) && (diffRot < angleEpsilon))
					{
						waypointTowardsIndex++;
						// UE.Debug.Log("go next waypoint: " + waypointTowardsIndex);
					}

					if (_script.loop && waypointTowardsIndex >= waypointTowards.Count)
					{
						StopWaypointFollowing();
						StartWaypointFollowing();
						// UE.Debug.Log("Loop again");
					}
				}
			}

			private void StartWaypointFollowing()
			{
				_followingWaypoint = true;
			}

			private void StopWaypointFollowing()
			{
				_followingWaypoint = false;
				waypointTowardsIndex = 0;
				elapsedTimeSinceAnimationStarted = 0;
			}

			private void RestartWayPointFollowing()
			{
				StopWaypointFollowing();

				if (waypointTowards.Count > 0)
				{
					var waypoint = waypointTowards[waypointTowardsIndex];
					SetActorPose(waypoint.tranlateTo, waypoint.rotateTo);
				}

				StartWaypointFollowing();
			}

			private UE.Pose CurrentActorPose()
			{
				return trgetPose;
			}

			private UE.Pose SetActorPose(in UE.Vector3 newPosition, in UE.Quaternion newRotation)
			{
				trgetPose.position = newPosition;
				trgetPose.rotation = newRotation;

				var initPose = GetAllPose();
				transform.localPosition = initPose.position + trgetPose.position;
				transform.localRotation = trgetPose.rotation * initPose.rotation;

				return CurrentActorPose();
			}

			private UE.Pose GetAllPose()
			{
				var totalPose = new UE.Pose(UE.Vector3.zero, UE.Quaternion.identity);

				for (var i = 0; i < GetPoseCount(); i++)
				{
					var pose = GetPose(i);
					totalPose.position += pose.position;
					totalPose.rotation *= pose.rotation;
				}

				return totalPose;
			}


			public void SetScript(in SDF.Actor.Script script)
			{
				_script = script;

				foreach (var trajectory in _script.trajectories)
				{
					// UE.Debug.LogFormat("id:{0} type:{1} tension:{2}", trajectory.id, trajectory.Type, trajectory.tension);
					if (trajectory.waypoints.Count > 0)
					{
						var lastPosition = UE.Vector3.zero;
						var lastRotation = UE.Quaternion.identity;
						var startIndex = 0;
						var firstWayPoint = trajectory.waypoints[0];

						if (firstWayPoint.time == 0)
						{
							lastPosition = SDF2Unity.GetPosition(firstWayPoint.Pose.Pos);
							lastRotation = SDF2Unity.GetRotation(firstWayPoint.Pose.Rot);
							startIndex = 1;
						}

						SetActorPose(lastPosition, lastRotation);

						var lastTime = 0f;
						for (var i = startIndex; i < trajectory.waypoints.Count; i++)
						{
							var waypoint = trajectory.waypoints[i];
							// UE.Debug.Log("Time: " + waypoint.time + ", Position: " + SDF2Unity.GetPosition(waypoint.Pose.Pos) + ", Rotation: " + SDF2Unity.GetRotation(waypoint.Pose.Rot));

							var waypointToward = new waypointToward();
							var nextTime = (float)waypoint.time;
							var nextPosition = SDF2Unity.GetPosition(waypoint.Pose.Pos);
							var nextRotation = SDF2Unity.GetRotation(waypoint.Pose.Rot);

							waypointToward.linearSpeed = UE.Vector3.Distance(nextPosition, lastPosition) / (nextTime - lastTime);
							waypointToward.angularSpeed = UE.Quaternion.Angle(nextRotation, lastRotation) / (nextTime - lastTime);
							waypointToward.tranlateTo = nextPosition;
							waypointToward.rotateTo = nextRotation;

							waypointTowards.Add(waypointToward);
							// UE.Debug.Log("\t Speed(linear/angular): (" + waypointToward.linearSpeed + "/" + waypointToward.angularSpeed + ", pos: " + waypointToward.tranlateTo + ", rot:" + waypointToward.rotateTo);

							lastTime = nextTime;
							lastPosition = nextPosition;
							lastRotation = nextRotation;
						}
					}
				}
			}
		}
	}
}
