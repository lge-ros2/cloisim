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
	// public static UE.Vector3[] GetBoundCornerPointsByExtents(in UE.Vector3 extents)
	// {
	// 	var cornerPoints = new UE.Vector3[] {
	// 						extents,
	// 						extents,
	// 						extents,
	// 						extents,
	// 						extents * -1,
	// 						extents * -1,
	// 						extents * -1,
	// 						extents * -1
	// 					};

	// 	cornerPoints[1].x *= -1;

	// 	cornerPoints[2].x *= -1;
	// 	cornerPoints[2].z *= -1;

	// 	cornerPoints[3].z *= -1;

	// 	cornerPoints[5].x *= -1;

	// 	cornerPoints[6].x *= -1;
	// 	cornerPoints[6].z *= -1;

	// 	cornerPoints[7].z *= -1;

	// 	return cornerPoints;
	// }

	public struct ObjectTracking
	{
		public UE.Transform rootTransform;
		public UE.Vector3 velocity;
		public UE.Vector3 position;
		public UE.Vector3 size;
		public List<UE.Vector3> footprint;

		public ObjectTracking(UE.GameObject gameObject)
		{
			rootTransform = gameObject.transform;
			velocity = UE.Vector3.zero;
			position = UE.Vector3.zero;
			size = UE.Vector3.zero;
			footprint = new List<UE.Vector3>();
		}

		// protected void SetFootPrint(in UE.Vector3[] cornerPoints)
		// {
		// 	foreach (var cornerPoint in cornerPoints)
		// 	{
		// 		// UE.Debug.Log(cornerPoint.ToString("F6"));
		// 		footprint.Add(cornerPoint);
		// 	}
		// }
	}

	private Dictionary<string, SDF.Helper.Base> allLoadedModelList = new Dictionary<string, SDF.Helper.Base>();
	private Dictionary<int, ObjectTracking> trackingObjectList = new Dictionary<int, ObjectTracking>();
	private messages.PerceptionV perceptions;
	private int sleepPeriodForPublishInMilliseconds = 1000;

	private UE.GameObject GetTrackingObject(in string modelName)
	{
		if (allLoadedModelList.TryGetValue(modelName, out var model))
		{
			return model.gameObject;
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
			perception.Size = new messages.Vector3d();

			var trackingGameObject = GetTrackingObject(target);
			if (trackingGameObject != null)
			{
				var trackingObject = new ObjectTracking(trackingGameObject);

				trackingObjectList.Add(trackingId, trackingObject);
				UE.Debug.LogFormat("TrackingObject: {0}, trackingId: {1}, classId: {2}", target, trackingId, classId);


				// {
				// 	var meshColliders = gameObject.GetComponentsInChildren<UE.MeshCollider>();
				// 	var combine = new UE.CombineInstance[meshColliders.Length];
				// 	for (var i = 0; i < combine.Length; i++)
				// 	{
				// 		combine[i].mesh = meshColliders[i].sharedMesh;
				// 		combine[i].transform = meshColliders[i].transform.localToWorldMatrix;
				// 	}

				// 	var combinedMesh = new UE.Mesh();
				// 	combinedMesh.CombineMeshes(combine, true, true);
				// 	combinedMesh.RecalculateBounds();
				// 	combinedMesh.Optimize();
				// 	// UE.Debug.Log(gameObject.name + ", " + combinedMesh.bounds.size + ", " + combinedMesh.bounds.extents+ ", " + combinedMesh.bounds.center);

				// 	var cornerPoints = GetBoundCornerPointsByExtents(combinedMesh.bounds.extents);
				// 	SetFootPrint(cornerPoints);
				// }

				// var capsuleCollider = gameObject.GetComponentInChildren<UE.CapsuleCollider>();

				// if (capsuleCollider != null)
				// {
				// 	var bounds = capsuleCollider.bounds;
				// 	var cornerPoints = GetBoundCornerPointsByExtents(bounds.extents);
				// 	SetFootPrint(cornerPoints);
				// }


				foreach (var footprint in trackingObject.footprint)
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

	protected override void OnReset()
	{
		foreach (var trackingObjectItem in trackingObjectList)
		{
			var trackingObject = trackingObjectItem.Value;
			trackingObject.velocity = UE.Vector3.zero;
			trackingObject.position = transform.position;
		}
	}

	void Update()
	{
		foreach (var trackingObjectItem in trackingObjectList)
		{
			var trackingObject = trackingObjectItem.Value;
			if (trackingObject.rootTransform != null)
			{
				var newPosition = trackingObject.rootTransform.position;
				trackingObject.velocity = (newPosition - trackingObject.position) / UE.Time.deltaTime;
				trackingObject.position = newPosition;
				// UE.Debug.Log(trackingObject.name + ": " + trackingObject.Velocity + ", " + trackingObject.Position);
			}
		}
	}

	protected void PublishThread()
	{
		var deviceMessage = new DeviceMessage();
		while (IsRunningThread)
		{
			foreach (var perception in perceptions.Perceptions)
			{
				if (trackingObjectList.TryGetValue(perception.TrackingId, out var trackingObject))
				{
					DeviceHelper.SetCurrentTime(perception.Header.Stamp);
					DeviceHelper.SetVector3d(perception.Position, trackingObject.position);
					DeviceHelper.SetVector3d(perception.Velocity, trackingObject.velocity);
				}
			}

			DeviceHelper.SetCurrentTime(perceptions.Header.Stamp);
			deviceMessage.SetMessage<messages.PerceptionV>(perceptions);
			Publish(deviceMessage);

			SleepThread(sleepPeriodForPublishInMilliseconds);
		}
	}
}