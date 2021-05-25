/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(600)]
public class ActorPlugin : CLOiSimPlugin
{
	public static Dictionary<string, List<SDF.Helper.Model>> StaticModelList = new Dictionary<string, List<SDF.Helper.Model>>();

	private static void UpdateStaticModelList()
	{
		if (StaticModelList.Count == 0)
		{
			var models = Main.WorldRoot.GetComponentsInChildren<SDF.Helper.Model>();
			foreach (var model in models)
			{
				if (model.isStatic)
				{
					if (StaticModelList.TryGetValue(model.name, out var list))
					{
						list.Add(model);
					}
					else
					{
						var newModelList = new List<SDF.Helper.Model>() { model };
						StaticModelList.Add(model.name, newModelList);
					}
				}
			}
		}
		else
		{
			Debug.LogWarning("Already static model list updated.");
		}
	}

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partName = "actorplugin";
	}

	protected override void OnStart()
	{
		UpdateStaticModelList();

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
			// Debug.Log("zone model: " + zone);
			if (StaticModelList.TryGetValue(zone, out var modelList))
			{
				foreach (var model in modelList)
				{
					var meshSourceTag = model.gameObject.AddComponent<NavMeshSourceTag>();
					Main.WorldNavMeshBuilder.AddNavMeshTracks(model.transform, meshSourceTag);
				}
			}
		}

		Main.WorldNavMeshBuilder.UpdateNavMesh(false);

		var actorAgent = gameObject.AddComponent<ActorAgent>();
		var motionSpeed = GetPluginParameters().GetValue<float>("default/steering/speed", 1.0f);
		var motionAngularSpeed = GetPluginParameters().GetValue<float>("default/steering/angular_speed", 2.09f) * Mathf.Rad2Deg;
		var motionAcceleration = GetPluginParameters().GetValue<float>("default/steering/acceleration", 8.0f);

		// Debug.Log("speed:" + motionSpeed + ", angularspeed: " + motionAngularSpeed + ", acceleration: " + motionAcceleration);
		actorAgent.SetSteering(motionSpeed, motionAngularSpeed, motionAcceleration);

		var motionStandby = GetPluginParameters().GetValue<string>("motion/standby");
		var motionMoving = GetPluginParameters().GetValue<string>("motion/moving");

		actorAgent.SetMotionType(ActorAgent.Type.STANDBY, motionStandby);
		actorAgent.SetMotionType(ActorAgent.Type.MOVING, motionMoving);

		actorAgent.RandomWalking = (defaultMotion.Equals("assign")) ? false : true;

		// actorAgent.AssignTargetDestination(new Vector3(35.8297f, -6.361397f, -35.49258f));
	}
}