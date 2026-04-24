/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class LogicalCamera : Device
	{
		[Header("Frustum Parameters")]
		[SerializeField]
		private float _near = 0f;

		[SerializeField]
		private float _far = 1f;

		[SerializeField]
		private float _aspectRatio = 1f;

		[SerializeField]
		private float _horizontalFov = 1f;

		public float Near
		{
			get => _near;
			set => _near = value;
		}

		public float Far
		{
			get => _far;
			set => _far = value;
		}

		public float AspectRatio
		{
			get => _aspectRatio;
			set => _aspectRatio = value;
		}

		public float HorizontalFov
		{
			get => _horizontalFov;
			set => _horizontalFov = value;
		}

		private messages.LogicalCameraImage _logicalCameraImage = null;
		private readonly List<messages.LogicalCameraImage.Model> _pooledModels = new();

		private Plane[] _frustumPlanes = new Plane[6];
		private List<SDFormat.Helper.Model> _candidateModels = new();

		// Cached transform data for thread-safe access from GenerateMessage()
		private Vector3 _cachedSensorPosition;
		private Quaternion _cachedSensorRotation;

		private struct DetectedModelData
		{
			public string Name;
			public Vector3 RelativePosition;
			public Quaternion RelativeRotation;
		}

		private readonly List<DetectedModelData> _cachedModelData = new();

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
		}

		protected override void OnStart()
		{
#if UNITY_EDITOR
			SceneVisibilityManager.instance.Show(gameObject, false);
#endif
		}

		protected override void OnReset()
		{
			lock (_cachedModelData)
			{
				_cachedModelData.Clear();
			}
		}

		protected override void InitializeMessages()
		{
			_logicalCameraImage = new messages.LogicalCameraImage
			{
				Header = new messages.Header
				{
					Stamp = new messages.Time()
				},
				Pose = new messages.Pose
				{
					Position = new messages.Vector3d(),
					Orientation = new messages.Quaternion()
				}
			};
		}

		protected override void SetupMessages()
		{
		}

		private void BuildFrustumPlanes()
		{
			var sensorTransform = transform;
			var position = sensorTransform.position;
			var forward = sensorTransform.forward;
			var up = sensorTransform.up;
			var right = sensorTransform.right;

			// Near plane: faces inward (forward direction)
			_frustumPlanes[0] = new Plane(forward, position + forward * _near);

			// Far plane: faces inward (backward direction)
			_frustumPlanes[1] = new Plane(-forward, position + forward * _far);

			// Calculate half-extents at the near plane
			var halfHFov = _horizontalFov * 0.5f;
			var tanHalfHFov = Mathf.Tan(halfHFov);
			var halfVFov = Mathf.Atan(tanHalfHFov / _aspectRatio);

			// Left plane
			_frustumPlanes[2] = new Plane(
				(Quaternion.AngleAxis(90f - Mathf.Rad2Deg * halfHFov, up) * -right).normalized,
				position);

			// Right plane
			_frustumPlanes[3] = new Plane(
				(Quaternion.AngleAxis(-(90f - Mathf.Rad2Deg * halfHFov), up) * right).normalized,
				position);

			// Top plane
			_frustumPlanes[4] = new Plane(
				(Quaternion.AngleAxis(90f - Mathf.Rad2Deg * halfVFov, right) * up).normalized,
				position);

			// Bottom plane
			_frustumPlanes[5] = new Plane(
				(Quaternion.AngleAxis(-(90f - Mathf.Rad2Deg * halfVFov), right) * -up).normalized,
				position);
		}

		private bool IsInsideFrustum(in Vector3 point)
		{
			for (var i = 0; i < 6; i++)
			{
				if (!_frustumPlanes[i].GetSide(point))
				{
					return false;
				}
			}
			return true;
		}

		private bool IsInsideFrustum(in Bounds bounds)
		{
			return GeometryUtility.TestPlanesAABB(_frustumPlanes, bounds);
		}

		void LateUpdate()
		{
			var sensorTransform = transform;

			BuildFrustumPlanes();

			_candidateModels.Clear();

			var worldRoot = Main.WorldRoot;
			if (worldRoot != null)
			{
				worldRoot.GetComponentsInChildren(false, _candidateModels);
			}

			lock (_cachedModelData)
			{
				_cachedSensorPosition = sensorTransform.position;
				_cachedSensorRotation = sensorTransform.rotation;
				_cachedModelData.Clear();

				for (var i = 0; i < _candidateModels.Count; i++)
				{
					var model = _candidateModels[i];
					if (!model.IsFirstChild)
						continue;

					if (model.gameObject == this.gameObject)
						continue;

					// Skip parent models that contain this sensor
					if (sensorTransform.IsChildOf(model.transform))
						continue;

					var modelPosition = model.transform.position;
					if (IsInsideFrustum(modelPosition))
					{
						var modelTransform = model.transform;
						_cachedModelData.Add(new DetectedModelData
						{
							Name = model.name,
							RelativePosition = sensorTransform.InverseTransformPoint(modelTransform.position),
							RelativeRotation = Quaternion.Inverse(_cachedSensorRotation) * modelTransform.rotation
						});
					}
				}

				// Detect props (spawned primitives under PropsRoot)
				var propsRoot = Main.PropsRoot;
				if (propsRoot != null)
				{
					var propsTransform = propsRoot.transform;
					for (var i = 0; i < propsTransform.childCount; i++)
					{
						var propTransform = propsTransform.GetChild(i);
						if (!propTransform.gameObject.activeInHierarchy)
							continue;

						var propPosition = propTransform.position;
						if (IsInsideFrustum(propPosition))
						{
							_cachedModelData.Add(new DetectedModelData
							{
								Name = propTransform.name,
								RelativePosition = sensorTransform.InverseTransformPoint(propPosition),
								RelativeRotation = Quaternion.Inverse(_cachedSensorRotation) * propTransform.rotation
							});
						}
					}
				}
			}

		}

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			var sensorTransform = transform;
			var position = sensorTransform.position;
			var forward = sensorTransform.forward;
			var up = sensorTransform.up;
			var right = sensorTransform.right;

			var halfHFov = _horizontalFov * 0.5f;
			var tanHalfHFov = Mathf.Tan(halfHFov);
			var halfVFov = Mathf.Atan(tanHalfHFov / _aspectRatio);
			var tanHalfVFov = Mathf.Tan(halfVFov);

			var nearHalfWidth = _near * tanHalfHFov;
			var nearHalfHeight = _near * tanHalfVFov;
			var nearCenter = position + forward * _near;
			var nearTL = nearCenter + up * nearHalfHeight - right * nearHalfWidth;
			var nearTR = nearCenter + up * nearHalfHeight + right * nearHalfWidth;
			var nearBL = nearCenter - up * nearHalfHeight - right * nearHalfWidth;
			var nearBR = nearCenter - up * nearHalfHeight + right * nearHalfWidth;

			var farHalfWidth = _far * tanHalfHFov;
			var farHalfHeight = _far * tanHalfVFov;
			var farCenter = position + forward * _far;
			var farTL = farCenter + up * farHalfHeight - right * farHalfWidth;
			var farTR = farCenter + up * farHalfHeight + right * farHalfWidth;
			var farBL = farCenter - up * farHalfHeight - right * farHalfWidth;
			var farBR = farCenter - up * farHalfHeight + right * farHalfWidth;

			Gizmos.color = new Color(0f, 1f, 0f, 0.4f);

			// Near plane
			Gizmos.DrawLine(nearTL, nearTR);
			Gizmos.DrawLine(nearTR, nearBR);
			Gizmos.DrawLine(nearBR, nearBL);
			Gizmos.DrawLine(nearBL, nearTL);

			// Far plane
			Gizmos.DrawLine(farTL, farTR);
			Gizmos.DrawLine(farTR, farBR);
			Gizmos.DrawLine(farBR, farBL);
			Gizmos.DrawLine(farBL, farTL);

			// Connecting edges
			Gizmos.DrawLine(nearTL, farTL);
			Gizmos.DrawLine(nearTR, farTR);
			Gizmos.DrawLine(nearBL, farBL);
			Gizmos.DrawLine(nearBR, farBR);

			// Show detected models
			Gizmos.color = Color.red;
			lock (_cachedModelData)
			{
				for (var i = 0; i < _cachedModelData.Count; i++)
				{
					var worldPos = sensorTransform.TransformPoint(_cachedModelData[i].RelativePosition);
					Gizmos.DrawSphere(worldPos, 0.15f);
				}
			}
		}
#endif

		protected override void GenerateMessage()
		{
			_logicalCameraImage.Header.Stamp.SetCurrentTime();
			_logicalCameraImage.Models.Clear();

			lock (_cachedModelData)
			{
				_logicalCameraImage.Pose.Position.Set(_cachedSensorPosition);
				_logicalCameraImage.Pose.Orientation.Set(_cachedSensorRotation);

				while (_pooledModels.Count < _cachedModelData.Count)
				{
					_pooledModels.Add(new messages.LogicalCameraImage.Model
					{
						Pose = new messages.Pose
						{
							Position = new messages.Vector3d(),
							Orientation = new messages.Quaternion()
						}
					});
				}

				for (var i = 0; i < _cachedModelData.Count; i++)
				{
					var data = _cachedModelData[i];

					var modelMsg = _pooledModels[i];
					modelMsg.Name = data.Name;
					modelMsg.Pose.Position.Set(data.RelativePosition);
					modelMsg.Pose.Orientation.Set(data.RelativeRotation);

					_logicalCameraImage.Models.Add(modelMsg);
				}
			}

			PushDeviceMessage(_logicalCameraImage);
#if UNITY_EDITOR
			UpdateProfiler("LogicalCamera", _logicalCameraImage.Models.Count * sizeof(double) * 7);
#endif
		}

	}
}
