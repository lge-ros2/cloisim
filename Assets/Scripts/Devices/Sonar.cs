/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SensorDevices
{
	public partial class Sonar : Device
	{
		private gazebo.msgs.SonarStamped sonarStamped = null;

		public string geometry = string.Empty;

		[Range(0, 100)]
		public double rangeMin = 0.001f;

		[Range(0, 100)]
		public double rangeMax = 0.0f;

		[Range(0, 100)]
		public double radius = 0;

		private List<Vector3> meshSensorRegionVertices = new List<Vector3>();

		private float sensorTimeElapsed = 0.0f;

		private Transform sonarLink = null;

		private float sensorStartOffset = 0;

		protected override void OnStart()
		{
			deviceName = name;
			sonarLink = transform.parent;

			var visualMesh = sonarLink.GetComponentInChildren<MeshFilter>();
			sensorStartOffset = (visualMesh == null)? 0f:visualMesh.sharedMesh.bounds.max.y;

			// Create a new sensing area
			var meshCollider = gameObject.AddComponent<MeshCollider>();

			float sensorMeshOffset = 0;
			Mesh mesh = null;
			if (geometry.Equals("sphere"))
			{
				mesh = ProceduralMesh.CreateSphere((float)radius);
				sensorMeshOffset = (float)radius;
			}
			else
			{
				mesh = ProceduralMesh.CreateCone((float)radius, 0, (float)rangeMax, 10);
				sensorMeshOffset = (float)rangeMax/2;
			}

			TranslateDetectionArea(mesh, 0.001f + sensorStartOffset + sensorMeshOffset);

			meshCollider.sharedMesh = mesh;
			meshCollider.convex = true;
			meshCollider.isTrigger = true;

			// preserve the vertex points of the sensing area
			var localToWorld = sonarLink.localToWorldMatrix;
			for (var i = 0; i < meshCollider.sharedMesh.vertices.Length; i++)
			{
				var targetPoint = meshCollider.sharedMesh.vertices[i];
				var distance = (Vector3.zero - targetPoint).magnitude;
				if (distance < (float)rangeMin)
				{
					continue;
				}

				meshSensorRegionVertices.Add(meshCollider.sharedMesh.vertices[i]);
			}

			// const MeshColliderCookingOptions cookingOptions
			// 	= MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices;
			// meshCollider.cookingOptions = cookingOptions;
			// meshCollider.hideFlags |= HideFlags.NotEditable;

			InitializeMessages();
		}

		private void TranslateDetectionArea(Mesh mesh, in float offset)
		{
			var vertices = mesh.vertices;
			for (var i = 0; i < vertices.Length; i++)
			{
				vertices[i].y += offset;
			}
			mesh.vertices = vertices;
		}

		private void InitializeMessages()
		{
			sonarStamped = new gazebo.msgs.SonarStamped();
			sonarStamped.Time = new gazebo.msgs.Time();
			sonarStamped.Sonar = new gazebo.msgs.Sonar();
			sonarStamped.Sonar.WorldPose = new gazebo.msgs.Pose();
			sonarStamped.Sonar.WorldPose.Position = new gazebo.msgs.Vector3d();
			sonarStamped.Sonar.WorldPose.Orientation = new gazebo.msgs.Quaternion();
			sonarStamped.Sonar.Contact = new gazebo.msgs.Vector3d();

			var sonar = sonarStamped.Sonar;
			sonar.Frame = deviceName;
			sonar.Radius = radius;
			sonar.RangeMin = rangeMin;
			sonar.RangeMax = rangeMax;
		}

		protected override IEnumerator MainDeviceWorker()
		{
			var sw = new Stopwatch();
			while (true)
			{
				sw.Restart();
				GenerateMessage();
				sw.Stop();

				yield return new WaitForSeconds(WaitPeriod((float)sw.Elapsed.TotalSeconds));
			}
		}

		protected override void GenerateMessage()
		{
			var sonarPosition = sonarLink.position;
			var sonarRotation = sonarLink.rotation;
			var sonar = sonarStamped.Sonar;
			sonar.Frame = deviceName;
			DeviceHelper.SetVector3d(sonar.WorldPose.Position, sonarPosition);
			DeviceHelper.SetQuaternion(sonar.WorldPose.Orientation, sonarRotation);

			DeviceHelper.SetCurrentTime(sonarStamped.Time);
			SetMessageData<gazebo.msgs.SonarStamped>(sonarStamped);
		}

		void OnTriggerStay(Collider other)
		{
			if (meshSensorRegionVertices.Count == 0)
			{
				return;
			}

			if ((sensorTimeElapsed += Time.fixedDeltaTime) < UpdatePeriod * 2)
			{
				return;
			}

			sensorTimeElapsed = 0.0f;

			var sonar = sonarStamped.Sonar;
			var sensorStartPoint = sonarLink.position;
			sensorStartPoint.z += sensorStartOffset;

			var detectedRange = (float)rangeMax;
			var contactPoint = Vector3.zero;
			var contactDirection = Vector3.zero;
			var localToWorld = sonarLink.localToWorldMatrix;

			for (var i = 0; i < meshSensorRegionVertices.Count; i++)
			{
				var targetPoint = localToWorld.MultiplyPoint3x4(meshSensorRegionVertices[i]);
				var direction = (targetPoint - sensorStartPoint).normalized;
				if (Physics.Raycast(sensorStartPoint, direction, out var hitInfo))
				{
					// Debug.DrawRay(sensorStartPoint, direction, Color.magenta, 0.01f);
					// Debug.Log("Hit Point of contact: " + hitInfo.point);
					var hitPoint = hitInfo.point;
					var hitDistance = Vector3.Distance(sensorStartPoint, hitPoint);
					if ((hitDistance < detectedRange) && (hitDistance > (float)rangeMin))
					{
						// Debug.Log("Hit Point of contact: " + hitInfo.point + "|" + distance.ToString("F4"));
						detectedRange = hitDistance;
						contactDirection = direction;
						contactPoint = hitPoint;
					}
				}
			}

			sonar.Range = detectedRange;
			DeviceHelper.SetVector3d(sonar.Contact, contactPoint);
			// Debug.Log(other.name + " |Stay| " + "," + detectedRange.ToString("F5") + ", " + contactPoint);
		}

		void OnTriggerExit(Collider other)
		{
			var sonar = sonarStamped.Sonar;
			sonar.Range = (float)rangeMax;
			DeviceHelper.SetVector3d(sonar.Contact, Vector3.zero);
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
				var point = new Vector3((float)contactPoint.X, (float)contactPoint.Z, (float)contactPoint.Y);
				return point;
			}
			catch
			{
				return Vector3.zero;
			}
		}

		protected override IEnumerator OnVisualize()
		{
			const float visualUpdatePeriod = 0.01f;
			const float visualDrawDuration = visualUpdatePeriod * 1.01f;
			var sensorStartPoint = Vector3.zero;
			var waitForSeconds = new WaitForSeconds(visualUpdatePeriod);

			while (true)
			{
				sensorStartPoint.Set(sonarLink.position.x, sonarLink.position.y, sonarLink.position.z + sensorStartOffset);

				var direction = (GetDetectedPoint() - sensorStartPoint).normalized;
				var detectedRange = GetDetectedRange();

				if (detectedRange < rangeMax && !direction.Equals(Vector3.zero))
				{
					Debug.DrawRay(sensorStartPoint, direction * detectedRange, Color.magenta, visualDrawDuration);
				}
				yield return waitForSeconds;
			}
		}
	}
}