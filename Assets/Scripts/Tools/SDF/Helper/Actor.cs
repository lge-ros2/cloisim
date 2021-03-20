/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Actor : Base
		{
			[UE.Header("SDF Properties")]
			public bool isStatic = false;
			private SDF.Actor.Script _script = null;

			public float linearSpeed = 1; // Adjust the speed for the application.
    		public float angularSpeed = 5; // Angular speed in degrees per sec.

			private bool isLoop = false;
			private bool autoStart = false;
			private float delayStart = 0;

			new void Awake()
			{
				base.Awake();
			}

			void Start()
			{
			}

			void LateUpdate()
			{
				if (_script == null)
				{
					return;
				}

				if (_script.auto_start)
				{


				}

				// foreach (_script.trajectories)
				{
					var moveTo = UE.Vector3.one;
					var rotateTo = UE.Quaternion.identity;

					var currentPose = CurrentPose();
					// transform.position = UE.Vector3.MoveTowards(currentPose.position, moveTo, linearSpeed * UE.Time.deltaTime);
					// transform.rotation = UE.Quaternion.RotateTowards(currentPose.rotation, rotateTo, angularSpeed * UE.Time.deltaTime);
				}
			}

			private UE.Pose CurrentPose()
			{
				var currentPose = new UE.Pose(transform.localPosition, transform.localRotation);
				return currentPose;
			}

			public void SetScript(in SDF.Actor.Script script)
			{
				_script = script;
				isLoop = _script.loop;
				delayStart = (float)_script.delay_start;
				autoStart = _script.auto_start;

				foreach (var trajectory in _script.trajectories)
				{
					UE.Debug.LogFormat("id:{0} type:{1} tension:{2}", trajectory.id, trajectory.Type, trajectory.tension);
					foreach (var waypoint in trajectory.waypoints)
					{
						UE.Debug.Log("time: " + waypoint.time);
						UE.Debug.Log("position: " + SDF2Unity.GetPosition(waypoint.Pose.Pos) + ", rotation: " + SDF2Unity.GetRotation(waypoint.Pose.Rot));
					}
				}

				//   <loop>true</loop>
				//   <delay_start>0.000000</delay_start>
				//   <auto_start>true</auto_start>
				//   <trajectory id="0" type="walking">
				//     <waypoint>
				//       <time>0.000000</time>
				//       <pose>0.000000 1.000000 0.000000 0.000000 0.000000 0.000000</pose>
				//     </waypoint>
			}
		}
	}
}