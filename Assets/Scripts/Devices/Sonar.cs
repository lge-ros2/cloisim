/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class Sonar : Device
	{
		private static readonly float Margin = 0.001f;

		private messages.SonarStamped sonarStamped = null;

		public string geometry = string.Empty;

		[Range(0, 100)]
		public double rangeMin = 0.001f;

		[Range(0, 100)]
		public double rangeMax = 0.0f;

		[Range(0, 100)]
		public double radius = 0;

		public Vector3 _sensorStartPoint = Vector3.zero;

		private List<Vector3> _meshSensorRegionVertices = new List<Vector3>();

		private Transform _sonarLink = null;

		private float _sensorStartOffset = 0;

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
			if (geometry.Equals("sphere"))
			{
				mesh = ProceduralMesh.CreateSphere((float)radius);
				sensorMeshOffset = (float)radius;
			}
			else
			{
				mesh = ProceduralMesh.CreateCone((float)radius, 0, (float)rangeMax, 14);
				sensorMeshOffset = (float)rangeMax / 2;
			}

			var translationOffset = Margin + _sensorStartOffset + sensorMeshOffset; // + (float)rangeMin;
			TranslateDetectionArea(mesh, translationOffset);

			var meshCollider = gameObject.AddComponent<MeshCollider>();
			meshCollider.sharedMesh = mesh;
			meshCollider.convex = true;
			meshCollider.isTrigger = true;

			ResolveSensingArea(meshCollider.sharedMesh);

			var sonar = sonarStamped.Sonar;
			sonar.Frame = DeviceName;
			sonar.Radius = radius;
			sonar.RangeMin = rangeMin;
			sonar.RangeMax = rangeMax;
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
			sonarStamped = new messages.SonarStamped();
			sonarStamped.Time = new messages.Time();
			sonarStamped.Sonar = new messages.Sonar();
			sonarStamped.Sonar.WorldPose = new messages.Pose();
			sonarStamped.Sonar.WorldPose.Position = new messages.Vector3d();
			sonarStamped.Sonar.WorldPose.Orientation = new messages.Quaternion();
			sonarStamped.Sonar.Contact = new messages.Vector3d();
		}

		protected override void GenerateMessage()
		{
			var sonarPosition = _sonarLink.position;
			var sonarRotation = _sonarLink.rotation;

			sonarStamped.Time.SetCurrentTime();

			var sonar = sonarStamped.Sonar;
			sonar.Frame = DeviceName;
			sonar.WorldPose.Position.Set(sonarPosition);
			sonar.WorldPose.Orientation.Set(sonarRotation);
			PushDeviceMessage<messages.SonarStamped>(sonarStamped);
		}

		private void ResolveSensingArea(Mesh targetMesh)
		{
			// preserve the vertex points of the sensing area
			for (var i = 0; i < targetMesh.vertices.Length; i++)
			{
				var targetPoint = targetMesh.vertices[i];
				var distance = targetPoint.magnitude;
				if (distance < (float)rangeMin)
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

		private int _pingPongIndex = 0;
		private float _sensorTimeElapsed = 0.0f;

		void OnTriggerStay(Collider other)
		{
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
				if (Physics.Raycast(_sensorStartPoint, direction, out var hitInfo, (float)rangeMax))
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

					if ((hitDistance <= (float)rangeMax) && (hitDistance > (float)rangeMin))
					{
						// Debug.Log("Hit Point " + i + " of contacts: " + hitCollider.name + "," + hitInfo.point + "|" + hitDistance.ToString("F4"));
						detectedRange = hitDistance;
						contactDirection = direction;
						contactPoint = hitPoint;
					}
				}
			}

			var sonar = sonarStamped.Sonar;
			sonar.Range = detectedRange;
			sonar.Contact.Set(contactPoint);
			// Debug.Log($"{DeviceName}: |Stay| {detectedRange.ToString("F5")} | {contactPoint}");

			_pingPongIndex++;
			_pingPongIndex %= 2;
		}

		void OnTriggerExit(Collider other)
		{
			var sonar = sonarStamped.Sonar;
			sonar.Range = (float)rangeMax;
			sonar.Contact.Set(Vector3.zero);
			// Debug.Log(other.name + " |Exit| " + "," + sonar.Range.ToString("F5"));
		}

		public float GetDetectedRange()
		{
			try
			{
				return (float)sonarStamped.Sonar.Range;
			}
			catch
			{
				return float.PositiveInfinity;
			}
		}

		public Vector3 GetDetectedPoint()
		{
			try
			{
				var contactPoint = sonarStamped.Sonar.Contact;
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