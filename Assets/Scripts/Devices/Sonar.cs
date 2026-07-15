/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Unity.Collections;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class Sonar : Device
	{
		private static readonly float Margin = 0.001f;

		private messages.Sonar _sonar = null;

		private ConcurrentDictionary<EntityId, byte> _collisionMonitoringList = new();

		[SerializeField]
		private string _geometry = string.Empty;

		[SerializeField]
		private double _rangeMin = 0.001;

		[SerializeField]
		private double _rangeMax = 0.0;

		[SerializeField]
		private double _radius = 0;

		[SerializeField]
		private Vector3 _sensorStartPoint = Vector3.zero;

		private List<Vector3> _meshSensorRegionVertices = new();

		private Transform _sonarLink = null;

		private float _sensorStartOffset = 0;

		private float _sensorTimeElapsed = 0.0f;

		private float _detectedRange = float.NegativeInfinity;

		private NativeArray<RaycastCommand> _raycastCommands;
		private NativeArray<RaycastHit> _raycastResults;
		private bool _raycastArraysAllocated = false;

		public string Geometry
		{
			get => _geometry;
			set => _geometry = value;
		}

		public double RangeMin
		{
			get => _rangeMin;
			set => _rangeMin = value;
		}

		public double RangeMax
		{
			get => _rangeMax;
			set => _rangeMax = value;
		}

		public double Radius
		{
			get => _radius;
			set => _radius = value;
		}

		protected new void OnDestroy()
		{
			if (_raycastArraysAllocated)
			{
				_raycastCommands.Dispose();
				_raycastResults.Dispose();
				_raycastArraysAllocated = false;
			}
			base.OnDestroy();
		}

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			DeviceName = name;
			_sonarLink = transform.parent;
		}

		private void AllocateRaycastArrays()
		{
			var rayCount = _meshSensorRegionVertices.Count;
			if (rayCount > 0)
			{
				_raycastCommands = new NativeArray<RaycastCommand>(rayCount, Allocator.Persistent);
				_raycastResults = new NativeArray<RaycastHit>(rayCount, Allocator.Persistent);
				_raycastArraysAllocated = true;
			}
		}

		protected override void OnStart()
		{
			var visualMesh = _sonarLink.GetComponentInChildren<MeshFilter>();
			if (visualMesh != null && visualMesh.sharedMesh != null)
			{
				_sensorStartOffset = visualMesh.sharedMesh.bounds.max.y;
			}
			else
			{
				_sensorStartOffset = 0f;
				Debug.LogWarning($"{name}: missing visual mesh under sonar link '{_sonarLink?.name}', using zero start offset");
			}

			// Create a new sensing area
			Mesh mesh = null;
			var sensorMeshOffset = 0f;
			if (_geometry.Equals("sphere"))
			{
				mesh = ProceduralMesh.CreateSphere((float)_radius);
				sensorMeshOffset = (float)_radius;
			}
			else
			{
				mesh = ProceduralMesh.CreateCone((float)_radius, 0, (float)_rangeMax, 14);
				sensorMeshOffset = (float)_rangeMax / 2;
			}

			var translationOffset = Margin + _sensorStartOffset + sensorMeshOffset; // + (float)_rangeMin;
			TranslateDetectionArea(mesh, translationOffset);

			var meshCollider = gameObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = mesh;
			meshCollider.convex = true;
			meshCollider.isTrigger = true;

			ResolveSensingArea(meshCollider.sharedMesh);

			AllocateRaycastArrays();

			var sonar = _sonar;
			sonar.Radius = _radius;
			sonar.RangeMin = _rangeMin;
			sonar.RangeMax = _rangeMax;
			sonar.Range = (float)_rangeMax;
			sonar.Contact.Set(Vector3.zero);
		}

		protected override void OnReset()
		{
			_detectedRange = float.NegativeInfinity;

			var sonar = _sonar;
			sonar.Range = (float)_rangeMax;
			sonar.Contact.Set(Vector3.zero);
		}

		public override bool SupportsVisualize => true;

		protected override IEnumerator OnVisualize()
		{
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			while (true)
			{
				var detectedPoint = GetDetectedPoint();

				if (!detectedPoint.Equals(Vector3.zero))
				{
					var direction = (detectedPoint - _sensorStartPoint).normalized;
					var detectedRange = GetDetectedRange();
					Debug.DrawRay(_sensorStartPoint, direction * detectedRange, Color.blue, UpdatePeriod);
					// Debug.Log($"{name} direction={direction} detectedRange{detectedRange}");
				}

				yield return waitForSeconds;
			}
		}

		protected override void InitializeMessages()
		{
			_sonar = new messages.Sonar
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				},
				WorldPose = new messages.Pose
				{
					Position = new messages.Vector3d(),
					Orientation = new messages.Quaternion()
				},
				Contact = new messages.Vector3d()
			};
		}

		protected override void SetupMessages()
		{
			_sonar.Frame = DeviceName;
		}

		protected override void GenerateMessage()
		{
			var sonarPosition = _sonarLink.position;
			var sonarRotation = _sonarLink.rotation;

			_sonar.Header.Stamp.SetCurrentTime();

			_sonar.WorldPose.Position.Set(sonarPosition);
			_sonar.WorldPose.Orientation.Set(sonarRotation);
			PushDeviceMessage(_sonar);
		}

		private void ResolveSensingArea(Mesh targetMesh)
		{
			// preserve the vertex points of the sensing area
			for (var i = 0; i < targetMesh.vertices.Length; i++)
			{
				var targetPoint = targetMesh.vertices[i];
				var distance = targetPoint.magnitude;
				if (distance < (float)_rangeMin)
				{
					continue;
				}
				_meshSensorRegionVertices.Add(targetPoint);
			}
		}

		private void TranslateDetectionArea(Mesh mesh, in float offset)
		{
			var vertices = mesh.vertices;
			for (var i = 0; i < vertices.Length; i++)
			{
				vertices[i].y += offset;
				vertices[i].y *= -1;
			}
			mesh.vertices = vertices;
		}

		void OnTriggerEnter(Collider other)
		{
			_collisionMonitoringList.TryAdd(other.gameObject.GetEntityId(), 0);
		}

		void OnTriggerExit(Collider other)
		{
			_collisionMonitoringList.TryRemove(other.gameObject.GetEntityId(), out _);
		}

		void LateUpdate()
		{
			if (_collisionMonitoringList.IsEmpty)
			{
				OnReset();
			}
		}

		void FixedUpdate()
		{
			if (_meshSensorRegionVertices.Count == 0 || _collisionMonitoringList.IsEmpty)
			{
				return;
			}

			if ((_sensorTimeElapsed += Time.fixedDeltaTime) < UpdatePeriod)
			{
				return;
			}

			_sensorTimeElapsed = 0.0f;

			_detectedRange = float.NegativeInfinity;
			var contactPoint = Vector3.zero;
			var localToWorld = transform.localToWorldMatrix;

			_sensorStartPoint.Set(0, -(Margin + _sensorStartOffset), 0);
			_sensorStartPoint = transform.localRotation * _sensorStartPoint;
			_sensorStartPoint += localToWorld.GetPosition();

			var rayCount = _meshSensorRegionVertices.Count;

			if (!_raycastArraysAllocated || _raycastCommands.Length != rayCount)
			{
				if (_raycastArraysAllocated)
				{
					_raycastCommands.Dispose();
					_raycastResults.Dispose();
				}
				AllocateRaycastArrays();
			}

			var maxDist = (float)_rangeMax;
			var queryParams = new QueryParameters(-1, false, QueryTriggerInteraction.Ignore, false);

			for (var i = 0; i < rayCount; i++)
			{
				var targetPoint = localToWorld.MultiplyPoint3x4(_meshSensorRegionVertices[i]);
				var direction = (targetPoint - _sensorStartPoint).normalized;
				_raycastCommands[i] = new RaycastCommand(_sensorStartPoint, direction, queryParams, maxDist);
			}

			RaycastCommand.ScheduleBatch(_raycastCommands, _raycastResults, 32).Complete();

			for (var i = 0; i < rayCount; i++)
			{
				var hit = _raycastResults[i];
				if (hit.collider == null)
					continue;

				var hitCollider = hit.collider;

				// ignore itself
				if (hitCollider.name.Equals(name) && hitCollider.transform.parent.name.Equals(_sonarLink.name))
					continue;

				var hitDistance = hit.distance;
				if (hitDistance <= maxDist && hitDistance > (float)_rangeMin)
				{
					_detectedRange = hitDistance;
					contactPoint = hit.point;
				}
			}

			var sonar = _sonar;
			sonar.Range = _detectedRange;
			sonar.Contact.Set(contactPoint);
			// Debug.Log($"{DeviceName}: |Stay| {detectedRange.ToString("F5")} | {contactPoint}");
		}

		public float GetDetectedRange()
		{
			return _detectedRange;
		}

		public messages.Sonar GetSonar()
		{
			return _sonar;
		}

		public Vector3 GetDetectedPoint()
		{
			try
			{
				var point = _sonar.Contact.ToUnity();
				return point;
			}
			catch
			{
				return Vector3.zero;
			}
		}
	}
}