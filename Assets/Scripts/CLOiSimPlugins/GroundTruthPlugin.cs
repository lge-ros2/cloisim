/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System;
using UE = UnityEngine;
using messages = cloisim.msgs;

public class GroundTruthPlugin : CLOiSimPlugin
{
	private sealed class ObjectTracking
	{
		private readonly UE.Transform rootTransform;
		private readonly object footprintLock;
		public UE.Vector3 velocity;
		public UE.Vector3 position;
		public UE.Quaternion rotation;
		public UE.Vector3 size;
		private readonly List<UE.Vector3> footprint;
		private readonly List<UE.Vector3> rotatedFootprint;

		public ObjectTracking(UE.GameObject gameObject)
		{
			this.rootTransform = gameObject.transform;
			this.footprintLock = new object();
			this.velocity = UE.Vector3.zero;
			this.position = UE.Vector3.zero;
			this.rotation = UE.Quaternion.identity;
			this.size = UE.Vector3.zero;
			this.footprint = new();
			this.rotatedFootprint = new();
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
				var deltaTime = UE.Time.deltaTime;
				this.velocity = deltaTime > 0f ? (newPosition - this.position) / deltaTime : UE.Vector3.zero;
				this.position = newPosition;
				this.rotation = this.rootTransform.rotation;
				// UE.Debug.Log(this.rootTransform.name + ": " + this.Velocity + ", " + this.Position);

				lock (this.footprintLock)
				{
					for (var i = 0; i < this.footprint.Count; i++)
					{
						this.rotatedFootprint[i] = this.rotation * this.footprint[i];
					}
				}
			}
		}

		public int FootprintCount
		{
			get
			{
				lock (this.footprintLock)
				{
					return this.rotatedFootprint.Count;
				}
			}
		}

		public UE.Vector3[] Footprint()
		{
			lock (this.footprintLock)
			{
				return this.rotatedFootprint.ToArray();
			}
		}

		public void CopyFootprintTo(List<UE.Vector3> destination)
		{
			lock (this.footprintLock)
			{
				destination.Clear();
				if (destination.Capacity < this.rotatedFootprint.Count)
				{
					destination.Capacity = this.rotatedFootprint.Count;
				}
				destination.AddRange(this.rotatedFootprint);
			}
		}

		public void ClearFootprint()
		{
			lock (this.footprintLock)
			{
				this.footprint.Clear();
				this.rotatedFootprint.Clear();
			}
		}

		public void Set2DFootprint(in UE.Vector3[] vertices)
		{
			lock (this.footprintLock)
			{
				this.footprint.Clear();
				this.rotatedFootprint.Clear();
				this.footprint.AddRange(vertices);
				this.rotatedFootprint.AddRange(vertices);
			}
		}

		public void Add2DFootprint(in UE.Vector3 vertex)
		{
			lock (this.footprintLock)
			{
				this.footprint.Add(vertex);
				this.rotatedFootprint.Add(vertex);
			}
		}
	}

	private struct PropSnapshot
	{
		public UE.EntityId instanceId;
		public string name;
		public UE.Vector3 localPosition;
		public UE.Vector3 localScale;
	}

	private Dictionary<string, int> _propsClassId = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, SDF.Helper.Base> allLoadedModelList = new();
	private Dictionary<int, ObjectTracking> _trackingObjectList = new();
	private List<ObjectTracking> _trackingObjects = new();

	private messages.PerceptionV _messagePerceptions;
	private Dictionary<UE.EntityId, messages.Perception> _messagePerceptionProps = new();
	private List<messages.Perception> _messagePerceptionObjects = new();
	private List<UE.Vector3> _footprintScratchBuffer = new();
	private List<PropSnapshot> _propSnapshots = new();
	private HashSet<UE.EntityId> _activePropInstanceIds = new();
	private List<UE.EntityId> _stalePropInstanceIds = new();
	private Stack<UE.Transform> _propTraversalStack = new();

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
		_type = ICLOiSimPlugin.Type.GROUNDTRUTH;
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

	private void CalculateFootprint(ObjectTracking trackingObject)
	{
		var trackingGameObject = trackingObject.GetGameObject();
		trackingObject.ClearFootprint();

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
				UE.Object.Destroy(combinedMesh);
			}
		}
	}

	protected override IEnumerator OnStart()
	{
		var publishFrequency = GetPluginParameters().GetValue<float>("publish_frequency", 1);
		sleepPeriodForPublishInMilliseconds = (int)(1f / publishFrequency * 1000f);

		var enableProps = GetPluginParameters().GetAttributeInPath<bool>("props", "enable");
		if (enableProps)
		{
			SetPropClassId();
		}
		yield return null;

		AddPerceptionList();

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, PublishThread);
		}

		yield return DoUpdateFootprint();
	}

	protected override void OnReset()
	{
		for (var i = 0; i < _trackingObjects.Count; i++)
		{
			var trackingObject = _trackingObjects[i];
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
				var trackingObject = new ObjectTracking(trackingGameObject);
				_trackingObjectList.Add(trackingId, trackingObject);
				_trackingObjects.Add(trackingObject);
			}

			_messagePerceptionObjects.Add(perception);
		}
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
		var prevColor = UE.Gizmos.color;
		for (var i = 0; i < _trackingObjects.Count; i++)
		{
			var trackingObject = _trackingObjects[i];

			UE.Gizmos.color = UE.Color.red;
			UE.Gizmos.DrawSphere(trackingObject.position, 0.03f);

			UE.Gizmos.color = UE.Color.yellow;
			foreach (var vertex in trackingObject.Footprint())
			{
				UE.Gizmos.DrawSphere(vertex + trackingObject.position, 0.005f);
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
			if (_trackingObjectList.TryGetValue(trackingId, out var trackingObject))
			{
				CalculateFootprint(trackingObject);
				perception.Footprints.Capacity = trackingObject.FootprintCount;
			}
			else
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
		for (var i = 0; i < _trackingObjects.Count; i++)
		{
			_trackingObjects[i].Update();
		}

		UpdateProps();
	}

	private static bool TryParsePropNameAndId(string propObjectName, out string propName, out int propId)
	{
		var separatorIndex = propObjectName.LastIndexOf('-');
		if (separatorIndex <= 0 || separatorIndex >= propObjectName.Length - 1)
		{
			propName = string.Empty;
			propId = 0;
			return false;
		}

		propName = propObjectName.Substring(0, separatorIndex);
		return Int32.TryParse(propObjectName.Substring(separatorIndex + 1), out propId);
	}

	private bool TryCreatePropPerception(in PropSnapshot propSnapshot, out messages.Perception perception)
	{
		perception = null;
		if (!TryParsePropNameAndId(propSnapshot.name, out var propName, out var propId))
		{
			return false;
		}

		if (!_propsClassId.TryGetValue(propName, out var classId))
		{
			return false;
		}

		perception = new messages.Perception();
		perception.Header = new messages.Header();
		perception.Header.Stamp = new messages.Time();
		perception.TrackingId = propName.GetHashCode() + propId;
		perception.ClassId = classId;
		perception.Position = new messages.Vector3d();
		perception.Position.Set(propSnapshot.localPosition);
		perception.Velocity = new messages.Vector3d();
		perception.Size = new messages.Vector3d();
		perception.Size.SetScale(propSnapshot.localScale);
		return true;
	}

	private void CollectPropSnapshots()
	{
		_propSnapshots.Clear();
		_activePropInstanceIds.Clear();
		_propTraversalStack.Clear();

		if (Main.PropsRoot == null)
		{
			return;
		}

		_propTraversalStack.Push(Main.PropsRoot.transform);
		while (_propTraversalStack.Count > 0)
		{
			var current = _propTraversalStack.Pop();
			for (var i = 0; i < current.childCount; i++)
			{
				var child = current.GetChild(i);
				_propTraversalStack.Push(child);
				if (!child.CompareTag("Props"))
				{
					continue;
				}

				var instanceId = child.GetEntityId();
				_activePropInstanceIds.Add(instanceId);
				_propSnapshots.Add(new PropSnapshot
				{
					instanceId = instanceId,
					name = child.name,
					localPosition = child.localPosition,
					localScale = child.localScale,
				});
			}
		}
	}

	private void UpdateProps()
	{
		if (_propsClassId.Count == 0)
		{
			return;
		}

		CollectPropSnapshots();

		lock (_messagePerceptionProps)
		{
			var deltaTime = UE.Time.deltaTime;
			for (var i = 0; i < _propSnapshots.Count; i++)
			{
				var propSnapshot = _propSnapshots[i];
				if (_messagePerceptionProps.TryGetValue(propSnapshot.instanceId, out var perception))
				{
					var previousPosition = new UE.Vector3((float)perception.Position.X, (float)perception.Position.Y, (float)perception.Position.Z);
					var velocity = deltaTime > 0f ? (propSnapshot.localPosition - previousPosition) / deltaTime : UE.Vector3.zero;
					perception.Velocity.Set(velocity);
					perception.Position.Set(propSnapshot.localPosition);
					perception.Size.SetScale(propSnapshot.localScale);
				}
				else if (TryCreatePropPerception(in propSnapshot, out perception))
				{
					_messagePerceptionProps.Add(propSnapshot.instanceId, perception);
				}
			}

			_stalePropInstanceIds.Clear();
			foreach (var instanceId in _messagePerceptionProps.Keys)
			{
				if (!_activePropInstanceIds.Contains(instanceId))
				{
					_stalePropInstanceIds.Add(instanceId);
				}
			}

			for (var i = 0; i < _stalePropInstanceIds.Count; i++)
			{
				_messagePerceptionProps.Remove(_stalePropInstanceIds[i]);
			}
		}
	}

	private void PublishThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramObject.targetPort);

		if (publisher == null)
		{
			return;
		}

		var deviceMessage = new DeviceMessage();
		var perceptions = _messagePerceptions.Perceptions;
		while (PluginThread.IsRunning)
		{
			UpdatePerceptionObjects();

			perceptions.Clear();
			lock (_messagePerceptionProps)
			{
				var totalCount = _messagePerceptionObjects.Count + _messagePerceptionProps.Count;
				if (perceptions.Capacity < totalCount)
				{
					perceptions.Capacity = totalCount;
				}

				perceptions.AddRange(_messagePerceptionObjects);
				foreach (var propPerception in _messagePerceptionProps.Values)
				{
					perceptions.Add(propPerception);
				}
			}

			_messagePerceptions.Header.Stamp.SetCurrentTime();
			deviceMessage.SetMessage<messages.PerceptionV>(_messagePerceptions);
			publisher.Publish(deviceMessage);

			CLOiSimPluginThread.Sleep(sleepPeriodForPublishInMilliseconds);
			perceptions.Clear();
		}
		deviceMessage.Dispose();
	}

	private void UpdatePerceptionObjects()
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

				trackingObject.CopyFootprintTo(_footprintScratchBuffer);
				if (perception.Footprints.Capacity < _footprintScratchBuffer.Count)
				{
					perception.Footprints.Capacity = _footprintScratchBuffer.Count;
				}

				for (var i = 0; i < _footprintScratchBuffer.Count; i++)
				{
					if (i < perception.Footprints.Count)
					{
						perception.Footprints[i].Set(_footprintScratchBuffer[i]);
					}
					else
					{
						var point = new messages.Vector3d();
						point.Set(_footprintScratchBuffer[i]);
						perception.Footprints.Add(point);
					}
				}

				if (perception.Footprints.Count > _footprintScratchBuffer.Count)
				{
					perception.Footprints.RemoveRange(_footprintScratchBuffer.Count, perception.Footprints.Count - _footprintScratchBuffer.Count);
				}
			}
		}
	}
}