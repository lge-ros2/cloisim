/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

[DefaultExecutionOrder(600)]
public class ActorPlugin : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partsName = "actorplugin";
	}

	protected override void OnStart()
	{
		var actorHelper = GetComponent<SDF.Helper.Actor>();

		if (actorHelper.HasWayPoints)
		{
			Debug.LogError("Cannot load plugins(" + name + ") with actor trajectories: Check actor/script/trajectory/waypoint");
			return;
		}

		var defaultMotion = GetPluginParameters().GetValue<string>("default/motion", "random");
		if (!defaultMotion.Equals("random") && !defaultMotion.Equals("assign"))
		{
			Debug.LogWarningFormat("Failed to ActorPlugin: default motion type({0}) is invalid", defaultMotion);
			return;
		}


		GetPluginParameters().GetValues<string>("activity_zone/model", out var zoneList);

		foreach (var zone in zoneList)
		{
			Main.WorldNavMeshBuilder.AddNavMeshZone(zone);
		}

		Main.WorldNavMeshBuilder.UpdateNavMesh(false);

		var motionSpeed = GetPluginParameters().GetValue<float>("default/steering/speed", 1.0f);
		var motionAngularSpeed = GetPluginParameters().GetValue<float>("default/steering/angular_speed", 2.09f) * Mathf.Rad2Deg;
		var motionAcceleration = GetPluginParameters().GetValue<float>("default/steering/acceleration", 8.0f);

		// Debug.Log("speed:" + motionSpeed + ", angularspeed: " + motionAngularSpeed + ", acceleration: " + motionAcceleration);
		var actorAgent = gameObject.AddComponent<ActorAgent>();
		actorAgent.SetSteering(motionSpeed, motionAngularSpeed, motionAcceleration);

		var motionStandby = GetPluginParameters().GetValue<string>("motion/standby");
		var motionMoving = GetPluginParameters().GetValue<string>("motion/moving");

		actorAgent.SetMotionType(ActorAgent.Type.STANDBY, motionStandby);
		actorAgent.SetMotionType(ActorAgent.Type.MOVING, motionMoving);

		actorAgent.RandomWalking = (defaultMotion.Equals("assign")) ? false : true;

		// actorAgent.AssignTargetDestination(new Vector3(35.8297f, -6.361397f, -35.49258f));
	}
}