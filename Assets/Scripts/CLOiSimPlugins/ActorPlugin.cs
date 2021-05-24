/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.AI;

public class ActorPlugin : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partName = "actorplugin";

		// gameObject.AddComponent<NavMeshAgent>();
		var randomWalk = gameObject.AddComponent<NavAgent>();

		// var depthcam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		// if (depthcam is null)
		// {
		// 	ChangePluginType(ICLOiSimPlugin.Type.CAMERA);
		// 	targetDevice = gameObject.GetComponent<SensorDevices.Camera>();
		// }
		// else
		// {
		// 	ChangePluginType(ICLOiSimPlugin.Type.DEPTHCAMERA);
		// 	targetDevice = depthcam;
		// }

		// partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var defaultMotion = GetPluginParameters().GetValue<string>("default_motion", "standby");
		if (!defaultMotion.Equals("standby") && !defaultMotion.Equals("random_moving"))
		{
			defaultMotion = "standby";
		}
		// sleepPeriodForPublishInMilliseconds = (int)(1f / publishFrequency * 1000f);
		// GetPluginParameters().GetValues<string>("list/target", out var targetList);

		// foreach (var target in targetList)
		// {
		// 	var trackingId = GetPluginParameters().GetAttributeInPath<int>("list/target[text()='" + target + "']", "tracking_id");
		// 	var classId = GetPluginParameters().GetAttributeInPath<int>("list/ta


		// RegisterServiceDevice(subPartName + "Info");
		// RegisterTxDevice(subPartName + "Data");

		// AddThread(RequestThread);
		// AddThread(SenderThread, targetDevice);
	}
}