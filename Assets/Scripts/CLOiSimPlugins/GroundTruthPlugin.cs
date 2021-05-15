/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

public class GroundTruthPlugin : CLOiSimPlugin
{
	private UE.GameObject worldRoot = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.GROUNDTRUTH;

		modelName = "GroundTruth";
		partName = "cloisim";

		worldRoot = Main.WorldRoot;
	}

	protected override void OnStart()
	{
		GetPluginParameters();

		GetPluginParameters().GetValues<string>("list/target", out var targetList);

		foreach (var target in targetList)
		{
			var class_id = GetPluginParameters().GetAttributeInPath<int>("list/target[text()='" + target + "']", "class_id");
			UE.Debug.Log(target + ", " + class_id);
		}

		RegisterTxDevice("GroundTruth");

		AddThread(PublishThread);
	}

	protected void PublishThread()
	{
		while (IsRunningThread)
		{
			// if (device.PopDeviceMessage(out var dataStreamToSend))
			{
				// Publish(dataStreamToSend);
			}
		}
	}
}