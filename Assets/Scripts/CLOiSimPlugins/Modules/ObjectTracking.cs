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
	private float _velocitySmoothingFactor = 0.25f;

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
		// Note: _size and the cached footprint (_footPrint / _rotatedFootPrint) are
		// derived from mesh geometry and survive a simulation reset, so we deliberately
		// do not clear them here. Re-running CalculateFootprint() can take hundreds of
		// milliseconds per tracked object due to Mesh.CombineMeshes.
		_velocity = Vector3.zero;
		_position = Vector3.zero;
		_rotation = Quaternion.identity;
		_previousFixedPosition = (_rootTransform != null) ? _rootTransform.position : Vector3.zero;
	}

	public void UpdateVelocity(float deltaTime)
	{
		if (_rootTransform != null)
		{
			var newPosition = _rootTransform.position;
			var instantVelocity = deltaTime > 0f ? (newPosition - _previousFixedPosition) / deltaTime : Vector3.zero;
			_velocity = Vector3.Lerp(_velocity, instantVelocity, _velocitySmoothingFactor);
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
				var validMeshFilters = new List<MeshFilter>(meshFilters.Length);
				for (var i = 0; i < meshFilters.Length; i++)
				{
					var meshFilter = meshFilters[i];
					if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
						continue;

					validMeshFilters.Add(meshFilter);
				}

				if (validMeshFilters.Count == 0)
				{
					_size = Vector3.zero;
					return;
				}

				var initialRotation = _rootTransform.transform.rotation;

				// Compute world-space bounds from local AABB corners (8 per mesh) without
				// touching every vertex. Avoids the giant Mesh.CombineMeshes allocation that
				// dominated CalculateFootprint() (CombineMeshes + CombineVertices in profiler).
				var worldBounds = new Bounds();
				var worldBoundsInit = false;
				for (var i = 0; i < validMeshFilters.Count; i++)
				{
					var ltw = validMeshFilters[i].transform.localToWorldMatrix;
					var lb = validMeshFilters[i].sharedMesh.bounds;
					var lbMin = lb.min;
					var lbMax = lb.max;
					for (var c = 0; c < 8; c++)
					{
						var corner = new Vector3(
							((c & 1) == 0) ? lbMin.x : lbMax.x,
							((c & 2) == 0) ? lbMin.y : lbMax.y,
							((c & 4) == 0) ? lbMin.z : lbMax.z);
						var wp = ltw.MultiplyPoint3x4(corner);
						if (!worldBoundsInit)
						{
							worldBounds = new Bounds(wp, Vector3.zero);
							worldBoundsInit = true;
						}
						else
						{
							worldBounds.Encapsulate(wp);
						}
					}
				}

				var center = worldBounds.center;
				var downsampledVertices = DownsampleVertices2D(validMeshFilters, center, initialRotation);

				var convexHullMeshData = downsampledVertices.SolveConvexHull2D();
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
							prevPoint = convexHullMeshData[i];
						}
					}

					Set2DFootprint(lowConvexHullMeshData.ToArray());
				}

				_size = worldBounds.size;
			}
		}
	}

	/// <summary>
	/// Iterates each MeshFilter's vertices via Mesh.GetVertices (reusable buffer),
	/// transforms them to world space, projects to 2D (y=0), applies center offset
	/// and rotation, then reduces count via spatial grid bucketing so the convex
	/// hull receives at most a few thousand points instead of the full mesh.
	/// Avoids Mesh.CombineMeshes which copied the entire vertex set into a new mesh.
	/// </summary>
	private static Vector3[] DownsampleVertices2D(List<MeshFilter> meshFilters, Vector3 center, Quaternion rotation)
	{
		const float cellSize = 0.01f; // 1 cm grid
		var inv = 1f / cellSize;
		var seen = new HashSet<long>();
		var result = new List<Vector3>();
		var vertsBuffer = new List<Vector3>(1024);

		for (var m = 0; m < meshFilters.Count; m++)
		{
			var mf = meshFilters[m];
			mf.sharedMesh.GetVertices(vertsBuffer);
			var ltw = mf.transform.localToWorldMatrix;

			for (var i = 0; i < vertsBuffer.Count; i++)
			{
				var v = ltw.MultiplyPoint3x4(vertsBuffer[i]) - center;
				v.y = 0;
				v = rotation * v;

				var ix = (long)Mathf.FloorToInt(v.x * inv);
				var iz = (long)Mathf.FloorToInt(v.z * inv);
				var key = ix * 73856093L ^ iz * 19349663L;

				if (seen.Add(key))
				{
					result.Add(v);
				}
			}
		}

		return result.ToArray();
	}
}