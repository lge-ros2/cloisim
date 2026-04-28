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
using SDFormat;

public class GroundTruthPlugin : CLOiSimPlugin
{
	private struct PropSnapshot
	{
		public UE.EntityId instanceId;
		public string name;
		public UE.Vector3 localPosition;
		public UE.Vector3 localScale;
	}

	private Dictionary<string, int> _propsClassId = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, SDFormat.Helper.Base> _allLoadedModelList = new();
	private Dictionary<int, ObjectTracking> _trackingObjectList = new();
	private List<ObjectTracking> _trackingObjects = new();

	private Dictionary<int, string> _targetNameList = new();
	private float _lastCheckTime = 0f;
	private float _checkInterval = 1.0f;

	private List<SDFormat.Helper.Model> _tempModelList = new();
	private List<SDFormat.Helper.Actor> _tempActorList = new();

	private messages.PerceptionV _messagePerceptions;
	private Dictionary<UE.EntityId, messages.Perception> _messagePerceptionProps = new();
	private List<messages.Perception> _messagePerceptionObjects = new();
	private List<UE.Vector3> _footprintScratchBuffer = new();
	private List<PropSnapshot> _propSnapshots = new();
	private HashSet<UE.EntityId> _activePropInstanceIds = new();
	private List<UE.EntityId> _stalePropInstanceIds = new();
	private Stack<UE.Transform> _propTraversalStack = new();
	private float _propVelocitySmoothingFactor = 0.25f;

	private int sleepPeriodForPublishInMilliseconds = 1000;

	private UE.GameObject GetTrackingObject(in string modelName)
	{
		if (_allLoadedModelList.TryGetValue(modelName, out var model))
		{
			if (model != null)
			{
				return model.gameObject;
			}
			else
			{
				_allLoadedModelList.Remove(modelName);
			}
		}

		return null;
	}

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.GROUNDTRUTH;
		_modelName = "World";
		_partsName = GetType().Name;

		var worldRoot = Main.WorldRoot;
		foreach (var model in worldRoot.GetComponentsInChildren<SDFormat.Helper.Model>())
		{
			if (model.IsFirstChild)
			{
				_allLoadedModelList.Add(model.name, model);
				// UE.Debug.Log("GT -> add allLodadedModellist: " + model.name);
			}
		}

		foreach (var actor in worldRoot.GetComponentsInChildren<SDFormat.Helper.Actor>())
		{
			_allLoadedModelList.Add(actor.name, actor);
		}

		_messagePerceptions = new messages.PerceptionV();
		_messagePerceptions.Header = new messages.Header();
		_messagePerceptions.Header.Stamp = new messages.Time();
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
			trackingObject.Reset();
		}
		StartCoroutine(DoUpdateFootprint());
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

			_targetNameList[trackingId] = target;

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
	private float _gizmoColorAlpha = 0.8f;
	private void OnDrawGizmos()
	{
		var prevColor = UE.Gizmos.color;

		var redColor = UE.Color.red;
		redColor.a = _gizmoColorAlpha;
		var yellowColor = UE.Color.yellow;
		yellowColor.a = _gizmoColorAlpha;
		for (var i = 0; i < _trackingObjects.Count; i++)
		{
			var trackingObject = _trackingObjects[i];
	
			UE.Gizmos.color = redColor;
			UE.Gizmos.DrawSphere(trackingObject.Position, 0.02f);

			UE.Gizmos.color = yellowColor;
			foreach (var vertex in trackingObject.Footprint())
			{
				UE.Gizmos.DrawSphere(vertex + trackingObject.Position, 0.003f);
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
				trackingObject.CalculateFootprint();
				perception.Footprints.Capacity = trackingObject.FootprintCount;
			}

			// Yield each iteration to spread the heavy mesh work across frames
			yield return null;
		}
	}

	IEnumerator DoUpdateFootprint(int trackingId, ObjectTracking trackingObject)
	{
		yield return new UE.WaitForEndOfFrame();

		trackingObject.CalculateFootprint();
		foreach (var perception in _messagePerceptionObjects)
		{
			if (perception.TrackingId == trackingId)
			{
				perception.Footprints.Capacity = trackingObject.FootprintCount;
				break;
			}
		}
	}

	void FixedUpdate()
	{
		var deltaTime = UE.Time.fixedDeltaTime;
		for (var i = 0; i < _trackingObjects.Count; i++)
		{
			_trackingObjects[i].UpdateVelocity(deltaTime);
		}
	}

	void LateUpdate()
	{
		if (_messagePerceptionObjects.Count > _trackingObjectList.Count)
		{
			if (UE.Time.time - _lastCheckTime > _checkInterval)
			{
				_lastCheckTime = UE.Time.time;

				var worldRoot = Main.WorldRoot;
				if (worldRoot != null)
				{
					worldRoot.GetComponentsInChildren(false, _tempModelList);
					foreach (var m in _tempModelList)
					{
						if (m.IsFirstChild)
						{
							_allLoadedModelList[m.name] = m;
						}
					}

					worldRoot.GetComponentsInChildren(false, _tempActorList);
					foreach (var a in _tempActorList)
					{
						_allLoadedModelList[a.name] = a;
					}
				}

				foreach (var perception in _messagePerceptionObjects)
				{
					var trackingId = perception.TrackingId;
					if (!_trackingObjectList.ContainsKey(trackingId))
					{
						if (_targetNameList.TryGetValue(trackingId, out var targetName))
						{
							var trackingGameObject = GetTrackingObject(targetName);
							if (trackingGameObject != null)
							{
								var trackingObject = new ObjectTracking(trackingGameObject);
								_trackingObjectList.Add(trackingId, trackingObject);
								_trackingObjects.Add(trackingObject);

								StartCoroutine(DoUpdateFootprint(trackingId, trackingObject));
							}
						}
					}
				}
			}
		}

		for (var i = _trackingObjects.Count - 1; i >= 0; i--)
		{
			var trackingObject = _trackingObjects[i];
			if (!trackingObject.IsValid)
			{
				RemoveTrackingObject(i);
				continue;
			}
			trackingObject.Update();
		}

		UpdateProps();
	}

	private void RemoveTrackingObject(int index)
	{
		var trackingObject = _trackingObjects[index];
		_trackingObjects.RemoveAt(index);

		// Find and remove from _trackingObjectList
		for (var j = _messagePerceptionObjects.Count - 1; j >= 0; j--)
		{
			var perception = _messagePerceptionObjects[j];
			if (_trackingObjectList.TryGetValue(perception.TrackingId, out var obj) && obj == trackingObject)
			{
				_trackingObjectList.Remove(perception.TrackingId);
				break;
			}
		}
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
		return int.TryParse(propObjectName.Substring(separatorIndex + 1), out propId);
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

		perception = new();
		perception.Header = new();
		perception.Header.Stamp = new();
		perception.TrackingId = propName.GetHashCode() + propId;
		perception.ClassId = classId;
		perception.Position = new();
		perception.Position.Set(propSnapshot.localPosition);
		perception.Velocity = new();
		perception.Size = new();
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
					var previousPosition = SDF2Unity.Position(perception.Position.X, perception.Position.Y, perception.Position.Z);
					var instantVelocity = deltaTime > 0f ? (propSnapshot.localPosition - previousPosition) / deltaTime : UE.Vector3.zero;
					var smoothedVelocity = UE.Vector3.Lerp(
						SDF2Unity.Position(perception.Velocity.X, perception.Velocity.Y, perception.Velocity.Z),
						instantVelocity,
						_propVelocitySmoothingFactor);
					perception.Velocity.Set(smoothedVelocity);
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

	private void PublishThread(object threadObject)
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

				foreach (var perception in _messagePerceptionObjects)
				{
					if (_trackingObjectList.ContainsKey(perception.TrackingId))
					{
						perceptions.Add(perception);
					}
				}

				foreach (var propPerception in _messagePerceptionProps.Values)
				{
					perceptions.Add(propPerception);
				}
			}

			_messagePerceptions.Header.Stamp.SetCurrentTime();
			deviceMessage.SetMessage(_messagePerceptions);
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
				perception.Position.Set(trackingObject.Position);
				perception.Velocity.Set(trackingObject.Velocity);
				perception.Size.SetScale(trackingObject.Size);

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