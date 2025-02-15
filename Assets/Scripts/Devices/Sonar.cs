/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class Sonar : Device
	{
		private static readonly float Margin = 0.001f;

		private messages.SonarStamped _sonarStamped = null;

		private ConcurrentDictionary<int, int> _collisionMonitoringList = new ConcurrentDictionary<int, int>();

		[SerializeField]
		private string _geometry = string.Empty;

		[SerializeField]
		[Range(0, 100)]
		private double _rangeMin = 0.001f;

		[SerializeField]
		[Range(0, 100)]
		private double _rangeMax = 0.0f;

		[SerializeField]
		[Range(0, 100)]
		public double _radius = 0;

		[SerializeField]
		private Vector3 _sensorStartPoint = Vector3.zero;

		private List<Vector3> _meshSensorRegionVertices = new List<Vector3>();

		private Transform _sonarLink = null;

		private float _sensorStartOffset = 0;

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

		protected override void OnAwake()
		{
			Mode = ModeType.TX;
			DeviceName = name;
			_sonarLink = transform.parent;
		}

		protected override void OnStart()
		{
			var visualMesh = _sonarLink.GetComponentInChildren<MeshFilter>();
			_sensorStartOffset = (visualMesh == null) ? 0f : visualMesh.sharedMesh.bounds.max.y;

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

			var sonar = _sonarStamped.Sonar;
			sonar.Frame = DeviceName;
			sonar.Radius = _radius;
			sonar.RangeMin = _rangeMin;
			sonar.RangeMax = _rangeMax;
			sonar.Range = (float)_rangeMax;
			sonar.Contact.Set(Vector3.zero);
		}

		protected override void OnReset()
		{
			var sonar = _sonarStamped.Sonar;
			sonar.Range = (float)_rangeMax;
			sonar.Contact.Set(Vector3.zero);
		}

		protected override IEnumerator OnVisualize()
		{
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			while (true)
			{
				var detectedPoint = GetDetectedPoint();

				if (!detectedPoint.Equals(Vector3.zero))
				{
					var direction = (GetDetectedPoint() - _sensorStartPoint).normalized;
					var detectedRange = GetDetectedRange();
					Debug.DrawRay(_sensorStartPoint, direction * detectedRange, Color.blue, UpdatePeriod);
					// Debug.Log($"{name} direction={direction} detectedRange{detectedRange}");
				}

				yield return waitForSeconds;
			}
		}

		protected override void InitializeMessages()
		{
			_sonarStamped = new messages.SonarStamped();
			_sonarStamped.Time = new messages.Time();
			_sonarStamped.Sonar = new messages.Sonar();
			_sonarStamped.Sonar.WorldPose = new messages.Pose();
			_sonarStamped.Sonar.WorldPose.Position = new messages.Vector3d();
			_sonarStamped.Sonar.WorldPose.Orientation = new messages.Quaternion();
			_sonarStamped.Sonar.Contact = new messages.Vector3d();
		}

		protected override void GenerateMessage()
		{
			var sonarPosition = _sonarLink.position;
			var sonarRotation = _sonarLink.rotation;

			_sonarStamped.Time.SetCurrentTime();

			var sonar = _sonarStamped.Sonar;
			sonar.Frame = DeviceName;
			sonar.WorldPose.Position.Set(sonarPosition);
			sonar.WorldPose.Orientation.Set(sonarRotation);
			PushDeviceMessage<messages.SonarStamped>(_sonarStamped);
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
			_collisionMonitoringList.TryAdd(other.gameObject.GetInstanceID(), 0);
		}

		private int _pingPongIndex = 0;
		private float _sensorTimeElapsed = 0.0f;

		void OnTriggerStay(Collider other)
		{
			_collisionMonitoringList.AddOrUpdate(other.gameObject.GetInstanceID(), 0, (key, existingValues) => existingValues + 1);

			if (_meshSensorRegionVertices.Count == 0)
			{
				return;
			}

			if ((_sensorTimeElapsed += Time.fixedDeltaTime) < UpdatePeriod * 2)
			{
				return;
			}
			else
			{
				_sensorTimeElapsed = 0.0f;
			}

			var detectedRange = float.NegativeInfinity;
			var contactPoint = Vector3.zero;
			var contactDirection = Vector3.zero;
			var localToWorld = this.transform.localToWorldMatrix;

			_sensorStartPoint.Set(0, -(Margin + _sensorStartOffset), 0);
			_sensorStartPoint = this.transform.localRotation * _sensorStartPoint;
			_sensorStartPoint += localToWorld.GetPosition();

			// Debug.Log("Hit Points: " + _meshSensorRegionVertices.Count);
			for (var i = _pingPongIndex; i < _meshSensorRegionVertices.Count; i += 2)
			{
				var targetPoint = localToWorld.MultiplyPoint3x4(_meshSensorRegionVertices[i]);
				var direction = targetPoint - _sensorStartPoint;

				// Debug.DrawLine(_sensorStartPoint, targetPoint, Color.red, 0.5f);
				if (Physics.Raycast(_sensorStartPoint, direction, out var hitInfo, (float)_rangeMax))
				{
					// Debug.DrawRay(_sensorStartPoint, direction, Color.magenta, 0.01f);
					// Debug.Log("Hit Point of contact: " + hitInfo.point + " | " + _sensorStartPoint.ToString("F4"));

					var hitPoint = hitInfo.point;
					var hitDistance = Vector3.Distance(_sensorStartPoint, hitPoint);
					var hitCollider = hitInfo.collider;

					// Debug.Log($"Hit Point {hitCollider.name}<->{name} | {hitCollider.transform.parent.name}<->{_sonarLink.name}");
					// ignore itself
					if (hitCollider.name.Equals(name) && hitCollider.transform.parent.name.Equals(_sonarLink.name))
					{
						continue;
					}

					if ((hitDistance <= (float)_rangeMax) && (hitDistance > (float)_rangeMin))
					{
						// Debug.Log("Hit Point " + i + " of contacts: " + hitCollider.name + "," + hitInfo.point + "|" + hitDistance.ToString("F4"));
						detectedRange = hitDistance;
						contactDirection = direction;
						contactPoint = hitPoint;
					}
				}
			}

			var sonar = _sonarStamped.Sonar;
			sonar.Range = detectedRange;
			sonar.Contact.Set(contactPoint);
			// Debug.Log($"{DeviceName}: |Stay| {detectedRange.ToString("F5")} | {contactPoint}");

			_pingPongIndex++;
			_pingPongIndex %= 2;
		}

		void OnTriggerExit(Collider other)
		{
			_collisionMonitoringList.TryRemove(other.gameObject.GetInstanceID(), out var _);

			// Debug.Log(other.name + " |Exit| " + "," + sonar.Range.ToString("F5"));
		}

		void LateUpdate()
		{
			if (_collisionMonitoringList.Count > 0)
			{
				foreach (var elem in _collisionMonitoringList)
				{
					if (elem.Value < 0)
					{
						_collisionMonitoringList.TryRemove(elem.Key, out var _);
					}
					else
					{
						_collisionMonitoringList.AddOrUpdate(elem.Key, 0, (key, existingValues) => existingValues - 5);
					}
				}
			}
			else
			{
				OnReset();
			}
		}

		public float GetDetectedRange()
		{
			try
			{
				return (float)_sonarStamped.Sonar.Range;
			}
			catch
			{
				return float.PositiveInfinity;
			}
		}

		public messages.SonarStamped GetSonar()
		{
			return _sonarStamped;
		}

		public Vector3 GetDetectedPoint()
		{
			try
			{
				var contactPoint = _sonarStamped.Sonar.Contact;
				var point = SDF2Unity.Position(contactPoint.X, contactPoint.Y, contactPoint.Z);
				return point;
			}
			catch
			{
				return Vector3.zero;
			}
		}
	}
}