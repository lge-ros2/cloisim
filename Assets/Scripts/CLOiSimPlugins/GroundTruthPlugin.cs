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
	private Dictionary<string, SDF.Helper.Base> allLoadedModelList = new Dictionary<string, SDF.Helper.Base>();
	private Dictionary<int, SDF.Helper.Base> trackingObjectList = new Dictionary<int, SDF.Helper.Base>();
	private messages.PerceptionV perceptions;
	private int sleepPeriodForPublishInMilliseconds = 1000;

	private SDF.Helper.Base GetTrackingObject(in string modelName)
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
		foreach (var model in worldRoot.GetComponentsInChildren<SDF.Helper.Model>())
		{
			if (model.IsFirstChild)
			{
				allLoadedModelList.Add(model.name, model);
				// UE.Debug.Log("GT -> add allLodadedModellist: " + model.name);
			}
		}

		foreach (var actor in worldRoot.GetComponentsInChildren<SDF.Helper.Actor>())
		{
			allLoadedModelList.Add(actor.name, actor);
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

			var perception = new messages.Perception();
			perception.TrackingId = trackingId;
			perception.ClassId = classId;
			perception.Position = new messages.Vector3d();
			perception.Velocity = new messages.Vector3d();

			var trackingObject = GetTrackingObject(target);
			if (trackingObject != null)
			{
				trackingObjectList.Add(trackingId, trackingObject);
				UE.Debug.LogFormat("TrackingObject: {0}, trackingId:{1}, classId:{2}", target, trackingId, classId);

				foreach (var footprint in trackingObject.FootPrints)
				{
					var point = new messages.Vector3d();
					DeviceHelper.SetVector3d(point, footprint);
					perception.Footprints.Add(point);
				}
			}

			perceptions.Perceptions.Add(perception);
		}

		RegisterTxDevice("Data");

		AddThread(PublishThread);
	}

	void Update()
	{
		foreach (var trackingObjectItem in trackingObjectList)
		{
			var trackingObject = trackingObjectItem.Value;
			if (trackingObject != null)
			{
				var newPosition = trackingObject.transform.position;

				trackingObject.Velocity = (newPosition - trackingObject.Position) / UE.Time.deltaTime;
				trackingObject.Position = newPosition;
				// UE.Debug.Log(trackingObject.name + ": " + trackingObject.Velocity + ", " + trackingObject.Position);
			}
		}
	}

	protected void PublishThread()
	{
		var deviceMessage = new DeviceMessage();
		while (IsRunningThread)
		{
			DeviceHelper.SetCurrentTime(perceptions.Header.Stamp);

			foreach (var perception in perceptions.Perceptions)
			{
				if (trackingObjectList.TryGetValue(perception.TrackingId, out var model))
				{
					DeviceHelper.SetVector3d(perception.Position, model.Position);
					DeviceHelper.SetVector3d(perception.Velocity, model.Velocity);
				}
			}

			deviceMessage.SetMessage<messages.PerceptionV>(perceptions);
			Publish(deviceMessage);

			SleepThread(sleepPeriodForPublishInMilliseconds);
		}
	}
}