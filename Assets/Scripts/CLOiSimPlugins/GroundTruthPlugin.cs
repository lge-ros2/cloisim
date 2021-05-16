/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;
using messages = cloisim.msgs;

public class GroundTruthPlugin : CLOiSimPlugin
{
	private Dictionary<string, SDF.Helper.Model> allLoadedModelList = new Dictionary<string, SDF.Helper.Model>();
	private Dictionary<int, SDF.Helper.Model> trackingModelList = new Dictionary<int, SDF.Helper.Model>();
	private messages.PerceptionV perceptions;
	private int sleepPeriodForPublishInMilliseconds = 1000;

	private SDF.Helper.Model GetModel(in string modelName)
	{
		if (allLoadedModelList.TryGetValue(modelName, out var model))
		{
			return model;
		}

		return null;
	}

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.GROUNDTRUTH;

		modelName = "GroundTruth";
		partName = "cloisim";

		var worldRoot = Main.WorldRoot;
		var modelsInWorld = worldRoot.GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var model in modelsInWorld)
		{
			if (model.IsFirstChild)
			{
				allLoadedModelList.Add(model.name, model);
				// UE.Debug.Log("GT -> add allLodadedModellist: " + model.name);
			}
		}

		perceptions = new messages.PerceptionV();
		perceptions.Header = new messages.Header();
		perceptions.Header.Stamp = new messages.Time();
	}

	protected override void OnStart()
	{
		GetPluginParameters();

		var publishFrequency = GetPluginParameters().GetValue<float>("publish_frequency", 1);
		sleepPeriodForPublishInMilliseconds = (int)(1f / publishFrequency * 1000f);
		GetPluginParameters().GetValues<string>("list/target", out var targetList);

		foreach (var target in targetList)
		{
			var trackingId = GetPluginParameters().GetAttributeInPath<int>("list/target[text()='" + target + "']", "tracking_id");
			var classId = GetPluginParameters().GetAttributeInPath<int>("list/target[text()='" + target + "']", "class_id");

			UE.Debug.LogFormat("{0}: trackingId:{1}, classId:{2}", target, trackingId, classId);


			var perception = new messages.Perception();
			perception.TrackingId = trackingId;
			perception.ClassId = classId;
			perception.Position = new messages.Vector3d();
			perception.Velocity = new messages.Vector3d();
			perceptions.Perceptions.Add(perception);

			var model = GetModel(target);
			if (model != null)
			{
				trackingModelList.Add(trackingId, model);
			}
		}

		RegisterTxDevice("Data");

		AddThread(PublishThread);
	}

	protected void PublishThread()
	{
		var deviceMessage = new DeviceMessage();
		while (IsRunningThread)
		{
			DeviceHelper.SetCurrentTime(perceptions.Header.Stamp);

			foreach (var perception in perceptions.Perceptions)
			{
				if (trackingModelList.TryGetValue(perception.TrackingId, out var model))
				{
					DeviceHelper.SetVector3d(perception.Position, model.Position);
					DeviceHelper.SetVector3d(perception.Velocity, model.Velocity);

					foreach (var footprint in model.FootPrints)
					{
						var point = new messages.Vector3d();
						DeviceHelper.SetVector3d(point, footprint);
						perception.FootPrints.Add(point);
					}
				}
			}

			deviceMessage.SetMessage<messages.PerceptionV>(perceptions);
			Publish(deviceMessage);

			SleepThread(sleepPeriodForPublishInMilliseconds);
		}
	}
}