/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using UnityEngine;

public sealed class ObjectTracking
{
	private readonly Transform _rootTransform;
	private readonly object _footPrintLock;
	private Vector3 _velocity;
	private Vector3 _position;
	private Quaternion _rotation;
	private Vector3 _size;
	private readonly List<Vector3> _footPrint;
	private readonly List<Vector3> _rotatedFootPrint;
	private Vector3 _previousFixedPosition;

	public bool IsValid => _rootTransform != null;
	public Vector3 Velocity => _velocity;
	public Vector3 Position => _position;
	public Quaternion Rotation => _rotation;
	public Vector3 Size { get => _size; set => _size = value; }

	public ObjectTracking(GameObject gameObject)
	{
		_rootTransform = gameObject.transform;
		_footPrintLock = new object();
		_velocity = Vector3.zero;
		_position = Vector3.zero;
		_previousFixedPosition = (_rootTransform != null) ? _rootTransform.position : Vector3.zero;
		_rotation = Quaternion.identity;
		_size = Vector3.zero;
		_footPrint = new();
		_rotatedFootPrint = new();		
	}

	public void Reset()
	{
		_velocity = Vector3.zero;
		_position = Vector3.zero;
		_rotation = Quaternion.identity;
		_size = Vector3.zero;
		_previousFixedPosition = (_rootTransform != null) ? _rootTransform.position : Vector3.zero;

		ClearFootprint();
	}

	public void UpdateVelocity(float deltaTime)
	{
		if (_rootTransform != null)
		{
			var newPosition = _rootTransform.position;
			_velocity = deltaTime > 0f ? (newPosition - _previousFixedPosition) / deltaTime : Vector3.zero;
			_previousFixedPosition = newPosition;
		}
	}

	public void Update()
	{
		if (_rootTransform != null)
		{
			_position = _rootTransform.position;
			_rotation = _rootTransform.rotation;

			lock (_footPrintLock)
			{
				for (var i = 0; i < _footPrint.Count; i++)
				{
					_rotatedFootPrint[i] = _rotation * _footPrint[i];
				}
			}
		}
	}

	public int FootprintCount
	{
		get
		{
			lock (_footPrintLock)
			{
				return _rotatedFootPrint.Count;
			}
		}
	}

	public Vector3[] Footprint()
	{
		lock (_footPrintLock)
		{
			return _rotatedFootPrint.ToArray();
		}
	}

	public void CopyFootprintTo(List<Vector3> destination)
	{
		lock (_footPrintLock)
		{
			destination.Clear();
			if (destination.Capacity < _rotatedFootPrint.Count)
			{
				destination.Capacity = _rotatedFootPrint.Count;
			}
			destination.AddRange(_rotatedFootPrint);
		}
	}

	private void ClearFootprint()
	{
		lock (_footPrintLock)
		{
			_footPrint.Clear();
			_rotatedFootPrint.Clear();
		}
	}

	private void Set2DFootprint(in Vector3[] vertices)
	{
		lock (_footPrintLock)
		{
			_footPrint.Clear();
			_rotatedFootPrint.Clear();
			_footPrint.AddRange(vertices);
			_rotatedFootPrint.AddRange(vertices);
		}
	}

	private void Add2DFootprint(in Vector3 vertex)
	{
		lock (_footPrintLock)
		{
			_footPrint.Add(vertex);
			_rotatedFootPrint.Add(vertex);
		}
	}

	public void CalculateFootprint()
	{
		ClearFootprint();

		var capsuleCollider = _rootTransform.GetComponentInChildren<CapsuleCollider>();
		if (capsuleCollider != null && _rootTransform.CompareTag("Actor"))
		{
			var radius = capsuleCollider.radius;

			const float angleResolution = 0.34906585f;
			for (var theta = 0f; theta < Mathf.PI * 2; theta += angleResolution)
			{
				var x = Mathf.Cos(theta) * radius;
				var z = Mathf.Sin(theta) * radius;
				Add2DFootprint(new Vector3(x, 0, z));
			}

			_size = capsuleCollider.bounds.size;
		}
		else
		{
			var meshFilters = _rootTransform.GetComponentsInChildren<MeshFilter>();
			if (meshFilters != null && _rootTransform.CompareTag("Model"))
			{
				var initialRotation = _rootTransform.transform.rotation;
				var combine = new CombineInstance[meshFilters.Length];
				for (var i = 0; i < combine.Length; i++)
				{
					combine[i].mesh = meshFilters[i].sharedMesh;
					combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
				}

				var combinedMesh = new Mesh();
				combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
				combinedMesh.CombineMeshes(combine, true, true);
				combinedMesh.RecalculateBounds();
				// Debug.Log(gameObject.name + ", " + combinedMesh.bounds.size + ", " + combinedMesh.bounds.extents+ ", " + combinedMesh.bounds.center);
				// trackingGameObject.AddComponent<MeshFilter>().sharedMesh = combinedMesh;

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
					var lowConvexHullMeshData = new List<Vector3>();
					var prevPoint = convexHullMeshData[0];
					for (var i = 1; i < convexHullMeshData.Length; i++)
					{
						if (Vector3.Distance(prevPoint, convexHullMeshData[i]) > minimumDistance)
						{
							lowConvexHullMeshData.Add(convexHullMeshData[i]);
							// Debug.Log(convexHullMeshData[i].ToString("F8"));
							prevPoint = convexHullMeshData[i];
						}
					}
					// Debug.Log("convexhull _footPrint count: " + convexHullMeshData.Length + " => low: " + lowConvexHullMeshData.Count);

					Set2DFootprint(lowConvexHullMeshData.ToArray());
				}

				_size = combinedMesh.bounds.size;
				Object.Destroy(combinedMesh);
			}
		}
	}
}