/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UE = UnityEngine;
using messages = cloisim.msgs;

public class GroundTruthPlugin : CLOiSimPlugin
{
	public struct ObjectTracking
	{
		private UE.Transform rootTransform;
		public UE.Vector3 velocity;
		public UE.Vector3 position;
		public UE.Quaternion rotation;
		public UE.Vector3 size;
		private List<UE.Vector3> footprint;
		private ArrayList rotatedFootprint;

		public ObjectTracking(UE.GameObject gameObject)
		{
			this.rootTransform = gameObject.transform;
			this.velocity = UE.Vector3.zero;
			this.position = UE.Vector3.zero;
			this.rotation = UE.Quaternion.identity;
			this.size = UE.Vector3.zero;
			this.footprint = new List<UE.Vector3>();
			this.rotatedFootprint = new ArrayList();
		}

		public UE.GameObject GetGameObject()
		{
			return (this.rootTransform != null) ? this.rootTransform.gameObject : null;
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

				lock (this.rotatedFootprint.SyncRoot)
				{
					for (var i = 0; i < this.footprint.Count; i++)
					{
						rotatedFootprint[i] = this.rotation * footprint[i];
					}
				}
			}
		}

		public UE.Vector3[] Footprint()
		{
			UE.Vector3[] footprintList;
			lock (this.rotatedFootprint.SyncRoot)
			{
				footprintList = (UE.Vector3[])rotatedFootprint.ToArray(typeof(UE.Vector3));
			}
			return footprintList;
		}

		public void Set2DFootprint(in UE.Vector3[] vertices)
		{
			this.footprint.AddRange(vertices);
			this.rotatedFootprint.AddRange(vertices);
		}

		public void Add2DFootprint(in UE.Vector3 vertex)
		{
			this.footprint.Add(vertex);
			this.rotatedFootprint.Add(vertex);
		}
	}

	private Dictionary<string, int> _propsClassId = new Dictionary<string, int>();
	private Dictionary<string, SDF.Helper.Base> allLoadedModelList = new Dictionary<string, SDF.Helper.Base>();
	private Dictionary<int, ObjectTracking> _trackingObjectList = new Dictionary<int, ObjectTracking>();

	private messages.PerceptionV _messagePerceptions;
	private Dictionary<int, messages.Perception> _messagePerceptionProps = new Dictionary<int, messages.Perception>();
	private List<messages.Perception> _messagePerceptionObjects = new List<messages.Perception>();

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
		_modelName = "World";
		_partsName = this.GetType().Name;

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

		_messagePerceptions = new messages.PerceptionV();
		_messagePerceptions.Header = new messages.Header();
		_messagePerceptions.Header.Stamp = new messages.Time();
	}

	private void CalculateFootprint(ref ObjectTracking trackingObject)
	{
		var trackingGameObject = trackingObject.GetGameObject();

		var capsuleCollider = trackingGameObject.GetComponentInChildren<UE.CapsuleCollider>();
		if (capsuleCollider != null && trackingGameObject.CompareTag("Actor"))
		{
			var radius = capsuleCollider.radius;

			const float angleResolution = 0.34906585f;
			for (var theta = 0f; theta < UE.Mathf.PI * 2; theta += angleResolution)
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
			if (meshFilters != null && trackingGameObject.CompareTag("Model"))
			{
				var initialRotation = trackingGameObject.transform.rotation;
				var combine = new UE.CombineInstance[meshFilters.Length];
				for (var i = 0; i < combine.Length; i++)
				{
					combine[i].mesh = meshFilters[i].sharedMesh;
					combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
				}

				var combinedMesh = new UE.Mesh();
				combinedMesh.indexFormat = UE.Rendering.IndexFormat.UInt32;
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
					vertices[i] = initialRotation * vertices[i];
				}

				var convexHullMeshData = DeviceHelper.SolveConvexHull2D(vertices);
				if (convexHullMeshData.Length > 0)
				{
					const float minimumDistance = 0.065f;
					var lowConvexHullMeshData = new List<UE.Vector3>();
					var prevPoint = convexHullMeshData[0];
					for (var i = 1; i < convexHullMeshData.Length; i++)
					{
						if (UE.Vector3.Distance(prevPoint, convexHullMeshData[i]) > minimumDistance)
						{
							lowConvexHullMeshData.Add(convexHullMeshData[i]);
							// UE.Debug.Log(convexHullMeshData[i].ToString("F8"));
							prevPoint = convexHullMeshData[i];
						}
					}
					// UE.Debug.Log("convexhull footprint count: " + convexHullMeshData.Length + " => low: " + lowConvexHullMeshData.Count);

					trackingObject.Set2DFootprint(lowConvexHullMeshData.ToArray());
				}

				trackingObject.size = combinedMesh.bounds.size;
			}
		}
	}

	protected override void OnStart()
	{
		var publishFrequency = GetPluginParameters().GetValue<float>("publish_frequency", 1);
		sleepPeriodForPublishInMilliseconds = (int)(1f / publishFrequency * 1000f);

		var enableProps = GetPluginParameters().GetAttributeInPath<bool>("props", "enable");
		if (enableProps)
		{
			SetPropClassId();
		}

		AddPerceptionList();

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, PublishThread);
		}

		StartCoroutine(DoUpdateFootprint());
	}

	protected override void OnReset()
	{
		foreach (var trackingObjectItem in _trackingObjectList)
		{
			var trackingObject = trackingObjectItem.Value;
			trackingObject.velocity = UE.Vector3.zero;
			trackingObject.position = UE.Vector3.zero;
		}
	}

	private void SetPropClassId()
	{
		if (GetPluginParameters().GetValues<string>("props/prop", out var propList))
		{
			foreach (var propName in propList)
			{
				var classId = GetPluginParameters().GetAttributeInPath<int>("props/prop[text()='" + propName + "']", "class_id");
				_propsClassId.Add(propName.ToUpper(), classId);
			}
		}
	}

	private void AddPerceptionList()
	{
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
				_trackingObjectList.Add(trackingId, new ObjectTracking(trackingGameObject));
			}

			_messagePerceptionObjects.Add(perception);
		}
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		var prevColor = UE.Gizmos.color;
		foreach (var objectItem in _trackingObjectList)
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
#endif

	IEnumerator DoUpdateFootprint()
	{
		yield return new UE.WaitForEndOfFrame();

		for (var i = 0; i < _messagePerceptionObjects.Count; i++)
		{
			var perception = _messagePerceptionObjects[i];
			var trackingId = perception.TrackingId;
			try
			{
				var trackingObject = _trackingObjectList[trackingId];
				CalculateFootprint(ref trackingObject);

				perception.Footprints.Capacity = trackingObject.Footprint().Length;
			}
			catch
			{
				UE.Debug.LogWarning(trackingId + "(" + perception.ClassId + ") is wrong object to get");
				// foreach (var track in _trackingObjectList)
				// {
				// 	UE.Debug.Log(track.Key + ", " + track.Value.GetGameObject().name);
				// }
			}
		}
	}

	void LateUpdate()
	{
		var keys = _trackingObjectList.Keys.ToArray();
		foreach (var key in keys)
		{
			var trackingObject = _trackingObjectList[key];
			trackingObject.Update();
			_trackingObjectList[key] = trackingObject;
		}

		StartCoroutine(UpdateProps());
	}

	private IEnumerator UpdateProps()
	{
		var props = Main.PropsRoot.GetComponentsInChildren<UE.Transform>();

		lock (_messagePerceptionProps)
		{
			var propsInstanceIdlist = new HashSet<int>();
			foreach (var prop in props)
			{
				if (!prop.CompareTag("Props"))
				{
					continue;
				}

				var propNameSplitted = prop.name.Split('-');

				var propName = propNameSplitted[0];
				var propId = Int32.Parse(propNameSplitted[1]);
				var instanceId = prop.GetInstanceID();

				if (_messagePerceptionProps.TryGetValue(instanceId, out var perception))
				{
					perception.Position.Set(prop.localPosition);
					perception.Size.SetScale(prop.localScale);
				}
				else
				{
					if (_propsClassId.TryGetValue(propName, out var classId))
					{
						perception = new messages.Perception();
						perception.Header = new messages.Header();
						perception.Header.Stamp = new messages.Time();
						perception.TrackingId = propName.GetHashCode() + propId;
						perception.ClassId = classId;
						perception.Position = new messages.Vector3d();
						perception.Position.Set(prop.localPosition);
						perception.Velocity = new messages.Vector3d();
						perception.Size = new messages.Vector3d();
						perception.Size.SetScale(prop.localScale);

						_messagePerceptionProps.Add(instanceId, perception);
					}
				}

				propsInstanceIdlist.Add(instanceId);
			}

			// remove unused props
			for (var i = 0; i < _messagePerceptionProps.Keys.Count; i++)
			{
				var key = _messagePerceptionProps.Keys.ElementAt(i);
				if (!propsInstanceIdlist.Contains(key))
				{
					_messagePerceptionProps.Remove(key);
				}
			}
		}

		yield return null;
	}

	private void PublishThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramObject.targetPort);

		var deviceMessage = new DeviceMessage();
		if (publisher != null)
		{
			while (PluginThread.IsRunning)
			{
				UpdatePeceptionObjects();

				_messagePerceptions.Perceptions.AddRange(_messagePerceptionObjects);

				var capacity = _messagePerceptions.Perceptions.Capacity;
				lock (_messagePerceptionProps)
				{
					// UE.Debug.Log(capacity + " , " + _messagePerceptionObjects.Count  + " , " + _messagePerceptionProps.Count);
					if (capacity < _messagePerceptionObjects.Count + _messagePerceptionProps.Count)
					{
						capacity = _messagePerceptionObjects.Count + _messagePerceptionProps.Count;
					}
					_messagePerceptions.Perceptions.Capacity = capacity;
					_messagePerceptions.Perceptions.InsertRange(_messagePerceptionObjects.Count, _messagePerceptionProps.Values.ToList());
				}

				_messagePerceptions.Header.Stamp.SetCurrentTime();
				deviceMessage.SetMessage<messages.PerceptionV>(_messagePerceptions);
				publisher.Publish(deviceMessage);

				CLOiSimPluginThread.Sleep(sleepPeriodForPublishInMilliseconds);
				_messagePerceptions.Perceptions.Clear();
			}
		}
	}

	private void UpdatePeceptionObjects()
	{
		for (var index = 0; index < _messagePerceptionObjects.Count; index++)
		{
			var perception = _messagePerceptionObjects[index];
			if (_trackingObjectList.TryGetValue(perception.TrackingId, out var trackingObject))
			{
				perception.Header.Stamp.SetCurrentTime();
				perception.Position.Set(trackingObject.position);
				perception.Velocity.Set(trackingObject.velocity);
				perception.Size.SetScale(trackingObject.size);

				var footprint = trackingObject.Footprint();
				for (var i = 0; i < footprint.Length; i++)
				{
					var point = new messages.Vector3d();
					point.Set(footprint[i]);

					if (i < perception.Footprints.Count)
					{
						perception.Footprints[i] = point;
					}
					else
					{
						perception.Footprints.Add(point);
					}
				}
			}
		}
	}
}