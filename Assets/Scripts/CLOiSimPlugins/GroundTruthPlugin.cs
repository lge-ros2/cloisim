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
	private const bool DrawObjectsInfo = false;

	public struct ObjectTracking
	{
		private UE.Transform rootTransform;
		public UE.Vector3 velocity;
		public UE.Vector3 position;
		public UE.Quaternion rotation;
		public UE.Vector3 size;
		private List<UE.Vector3> footprint;
		private List<UE.Vector3> rotatedFootprint;

		public ObjectTracking(UE.GameObject gameObject)
		{
			this.rootTransform = gameObject.transform;
			this.velocity = UE.Vector3.zero;
			this.position = UE.Vector3.zero;
			this.rotation = UE.Quaternion.identity;
			this.size = UE.Vector3.zero;
			this.footprint = new List<UE.Vector3>();
			this.rotatedFootprint = new List<UE.Vector3>();
		}

		public void Update()
		{
			if (this.rootTransform != null)
			{
				var newPosition = this.rootTransform.position;
				this.velocity = (newPosition - this.position) / UE.Time.deltaTime;
				this.position = newPosition;
				this.rotation = this.rootTransform.rotation;
				// UE.Debug.Log(this.rootTransform.name + ": " + this.Velocity + ", " + this.Position);
			}
		}

		public List<UE.Vector3> Footprint()
		{
			rotatedFootprint.Clear();
			for (var i = 0; i < this.footprint.Count; i++)
			{
				rotatedFootprint.Add(this.rotation * footprint[i]);
			}
			return this.rotatedFootprint;
		}

		public void Set2DFootprint(in UE.Vector3[] vertices)
		{
			this.footprint.AddRange(vertices);
		}

		public void Add2DFootprint(in UE.Vector3 vertex)
		{
			this.footprint.Add(vertex);
		}
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

	private void OnDrawGizmos()
	{
		if (DrawObjectsInfo)
		{
			var prevColor = UE.Gizmos.color;
			foreach (var objectItem in trackingObjectList)
			{
				var trackingObject = objectItem.Value;

				UE.Gizmos.color = UE.Color.red;
				UE.Gizmos.DrawSphere(trackingObject.position, 0.05f);

				UE.Gizmos.color = UE.Color.yellow;
				foreach (var vertex in trackingObject.Footprint())
				{
					UE.Gizmos.DrawSphere(vertex + trackingObject.position, 0.015f);
				}
			}

			UE.Gizmos.color = prevColor;
		}
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
			perception.Header = new messages.Header();
			perception.Header.Stamp = new messages.Time();
			perception.TrackingId = trackingId;
			perception.ClassId = classId;
			perception.Position = new messages.Vector3d();
			perception.Velocity = new messages.Vector3d();
			perception.Size = new messages.Vector3d();

			var trackingGameObject = GetTrackingObject(target);
			if (trackingGameObject != null)
			{
				var trackingObject = new ObjectTracking(trackingGameObject);

				var capsuleCollider = trackingGameObject.GetComponentInChildren<UE.CapsuleCollider>();
				if (capsuleCollider != null)
				{
					var radius = capsuleCollider.radius;

					for (var theta = 0f; theta < UE.Mathf.PI * 2; theta += 0.17f)
					{
						var x = UE.Mathf.Cos(theta) * radius;
 						var z = UE.Mathf.Sin(theta) * radius;
						trackingObject.Add2DFootprint(new UE.Vector3(x, 0, z));
					}

					trackingObject.size = capsuleCollider.bounds.size;
				}
				else
				{
					var meshFilters = trackingGameObject.GetComponentsInChildren<UE.MeshFilter>();
					if (meshFilters != null)
					{
						var combine = new UE.CombineInstance[meshFilters.Length];
						for (var i = 0; i < combine.Length; i++)
						{
							combine[i].mesh = meshFilters[i].sharedMesh;
							combine[i].transform  = meshFilters[i].transform.localToWorldMatrix;
						}

						var combinedMesh = new UE.Mesh();
						combinedMesh.CombineMeshes(combine, true, true);
						combinedMesh.RecalculateBounds();
						combinedMesh.RecalculateNormals();
						combinedMesh.RecalculateTangents();
						combinedMesh.Optimize();
						// UE.Debug.Log(gameObject.name + ", " + combinedMesh.bounds.size + ", " + combinedMesh.bounds.extents+ ", " + combinedMesh.bounds.center);
						// trackingGameObject.AddComponent<UE.MeshFilter>().sharedMesh = combinedMesh;

						// move offset and projection to 2D
						var vertices = combinedMesh.vertices;
						for (var i = 0; i < vertices.Length; i++)
						{
							vertices[i] -= combinedMesh.bounds.center;
							vertices[i].y = 0;
						}

						var convexHullMeshData = DeviceHelper.SolveConvexHull2D(vertices);

						const int lowLevel = 10;
						var lowConvexHullMeshData = new UE.Vector3[convexHullMeshData.Length / lowLevel];
						for (var i = 0; i < convexHullMeshData.Length; i += lowLevel)
						{
							// UE.Debug.Log(convexHullMeshData[i].ToString("F8"));
							lowConvexHullMeshData[i / lowLevel] = convexHullMeshData[i];
						}

						trackingObject.Set2DFootprint(convexHullMeshData);

						trackingObject.size = combinedMesh.bounds.size;
					}
				}

				trackingObjectList.Add(trackingId, trackingObject);
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
			trackingObject.position = UE.Vector3.zero;
		}
	}

	void Update()
	{
		for (var i = 0; i < trackingObjectList.Count; i++)
		{
			var trackingObject = trackingObjectList[i];
			trackingObject.Update();


			trackingObjectList[i] = trackingObject;
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
					DeviceHelper.SetVector3d(perception.Size, trackingObject.size);

					foreach (var footprint in trackingObject.Footprint())
					{
						var point = new messages.Vector3d();
						DeviceHelper.SetVector3d(point, footprint);
						perception.Footprints.Add(point);
					}
				}
			}

			DeviceHelper.SetCurrentTime(perceptions.Header.Stamp);
			deviceMessage.SetMessage<messages.PerceptionV>(perceptions);
			Publish(deviceMessage);

			SleepThread(sleepPeriodForPublishInMilliseconds);
		}
	}
}