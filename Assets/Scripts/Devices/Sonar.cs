/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public partial class Sonar : Device
	{
		private messages.SonarStamped sonarStamped = null;

		public string geometry = string.Empty;

		[Range(0, 100)]
		public double rangeMin = 0.001f;

		[Range(0, 100)]
		public double rangeMax = 0.0f;

		[Range(0, 100)]
		public double radius = 0;

		public Vector3 sensorStartPoint = Vector3.zero;

		public double detectedDistance = 0;

		private List<Vector3> meshSensorRegionVertices = new List<Vector3>();

		private Transform sonarLink = null;

		private float sensorStartOffset = 0;

		protected override void OnAwake()
		{
			deviceName = name;
			sonarLink = transform.parent;
			adjustCapturingRate = 0.95f;
		}

		protected override void OnStart()
		{
			var visualMesh = sonarLink.GetComponentInChildren<MeshFilter>();
			sensorStartOffset = (visualMesh == null) ? 0f : visualMesh.sharedMesh.bounds.max.y;

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

			TranslateDetectionArea(mesh, 0.0005f + sensorStartOffset + sensorMeshOffset);

			meshCollider.sharedMesh = mesh;
			meshCollider.convex = true;
			meshCollider.isTrigger = true;

			ResolveSensingArea(meshCollider);

			// const MeshColliderCookingOptions cookingOptions
			// 	= MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices;
			// meshCollider.cookingOptions = cookingOptions;
			// meshCollider.hideFlags |= HideFlags.NotEditable;
		}

		protected override IEnumerator OnVisualize()
		{
			var waitForEndOfFrame = new WaitForEndOfFrame();
			var waitForSeconds = new WaitForSeconds(UpdatePeriod);

			while (true)
			{
				yield return waitForEndOfFrame;

				var direction = (GetDetectedPoint() - sensorStartPoint).normalized;
				var detectedRange = GetDetectedRange();

				if (detectedRange <= rangeMax && !direction.Equals(Vector3.zero))
				{
					Debug.DrawRay(sensorStartPoint, direction * detectedRange, Color.blue, UpdatePeriod);
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
			SetMessageData<messages.SonarStamped>(sonarStamped);
		}

		private void ResolveSensingArea(in MeshCollider meshCollider)
		{
			// preserve the vertex points of the sensing area
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

		private float sensorTimeElapsed = 0.0f;

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
			else
			{
				sensorTimeElapsed = 0.0f;
			}

			var detectedRange = (float)rangeMax;
			var contactPoint = Vector3.zero;
			var contactDirection = Vector3.zero;
			var localToWorld = sonarLink.localToWorldMatrix;

			sensorStartPoint.Set(0, -sensorStartOffset, 0);
			sensorStartPoint = localToWorld.MultiplyPoint3x4(sensorStartPoint);

			// Debug.Log("Hit Points: " + meshSensorRegionVertices.Count);
			for (var i = 0; i < meshSensorRegionVertices.Count; i++)
			{
				var targetPoint = localToWorld.MultiplyPoint3x4(meshSensorRegionVertices[i]);
				var direction = (targetPoint - sensorStartPoint);

				if (Physics.Raycast(sensorStartPoint, direction, out var hitInfo))
				{
					// Debug.DrawRay(sensorStartPoint, direction, Color.magenta, 0.01f);

					// Debug.Log("Hit Point of contact: " + hitInfo.point + " | " + sensorStartPoint.ToString("F4"));
					var hitPoint = hitInfo.point;
					var hitDistance = Vector3.Distance(sensorStartPoint, hitPoint);

					if ((hitDistance < detectedRange) && (hitDistance > (float)rangeMin))
					{
						// Debug.Log("Hit Point " + i + " of contact: " + hitInfo.point + "|" + hitDistance.ToString("F4"));
						detectedRange = hitDistance;
						contactDirection = direction;
						contactPoint = hitPoint;
					}
				}
			}

			detectedDistance = detectedRange;
			var sonar = sonarStamped.Sonar;
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
	}
}