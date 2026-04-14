/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using CLOiSim.Cloth;
using SDFormat;

[DefaultExecutionOrder(100)]
public class ClothPlugin : CLOiSimPlugin
{
	[Header("ClothPlugin Settings")]
	[SerializeField]
	private UnityEngine.Mesh _clothMesh;

	[SerializeField]
	private BurstCloth _cloth;

	[SerializeField]
	private ColliderBridge _colliderBridge;

	[SerializeField]
	private Transform _meshTransform;

	[SerializeField]
	private float _searchMargin = 10f;

	[SerializeField]
	private float _colliderUpdateInterval = 1.0f;

	private float _nextColliderUpdate;
	private Transform _clothRoot;
	private Transform _worldRoot;
	private Transform _propsRoot;
	private readonly HashSet<Transform> _currentColliderSet = new();
	private Vector3 _lastMeshWorldPosition;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.NONE;
	}

	protected override IEnumerator OnStart()
	{
		var pluginParams = GetPluginParameters();
		if (pluginParams == null)
		{
			Debug.LogError("ClothPlugin: Plugin parameters not found");
			yield break;
		}

		// Resolve target link from "model::link" or just "link"
		var target = pluginParams.GetValue<string>("target");
		var linkTransform = ResolveTargetTransform(target);
		if (linkTransform == null)
		{
			Debug.LogError($"ClothPlugin: Cannot resolve target link '{target}'");
			yield break;
		}

		// if (!linkTransform.TryGetComponent(out _cloth))
		// 	_cloth = linkTransform.gameObject.AddComponent<BurstCloth>();
		_cloth = transform.gameObject.AddComponent<BurstCloth>();

		// Find the visual MeshFilter on the target link
		var meshFilter = linkTransform.GetComponentInChildren<MeshFilter>();
		if (meshFilter == null || meshFilter.sharedMesh == null)
		{
			Debug.LogError($"ClothPlugin: No mesh found under target link '{target}'");
			yield break;
		}

		// Instantiate the mesh so we can modify it at runtime
		_clothMesh = Object.Instantiate(meshFilter.sharedMesh);
		_clothMesh.name = meshFilter.sharedMesh.name + "_cloth";
		_clothMesh.MarkDynamic();
		meshFilter.mesh = _clothMesh;

		_meshTransform = meshFilter.transform;

		// Wait for model deployment to finish (user clicks to place the model)
		// so that world-space vertex positions are captured at the final location.
		yield return new WaitUntil(() =>
		{
			var modelHelper = linkTransform.GetComponentInParent<SDFormat.Helper.Model>();
			if (modelHelper == null) return true;
			var rootModel = modelHelper.RootModel ?? modelHelper;
			var artBody = rootModel.GetComponentInChildren<ArticulationBody>();
			// Model is deployed when ArticulationBody is no longer immovable, or doesn't exist
			return artBody == null || !artBody.immovable;
		});

		// Sync transforms one more frame to ensure final positions are applied
		yield return null;

		_clothRoot = linkTransform;
		_worldRoot = Main.WorldRoot.transform;
		_propsRoot = Main.PropsRoot.transform;
		_searchMargin = pluginParams.GetValue<float>("cloth/collider/search_margin", _searchMargin);
		_colliderUpdateInterval = pluginParams.GetValue<float>("cloth/collider/update_interval", _colliderUpdateInterval);

		ConfigureClothFromMesh(pluginParams);

		// Setup collider bridge with auto-discovered scene colliders
		_colliderBridge = transform.gameObject.AddComponent<ColliderBridge>();

		var sceneColliders = DiscoverSceneColliders();
		_colliderBridge.Initialize(_cloth, sceneColliders);
		_currentColliderSet.UnionWith(sceneColliders);
		_nextColliderUpdate = Time.time + _colliderUpdateInterval;
		_lastMeshWorldPosition = _meshTransform.position;

		StartSummary.AppendLine($"Cloth initialized from mesh '{_clothMesh.name}': " +
			$"{_clothMesh.vertexCount} vertices, {sceneColliders.Length} scene colliders");

		yield return null;
	}

	protected override void OnReset()
	{
		_cloth?.ResetToInitialState();
	}

	private void Update()
	{
		if (_cloth == null || _meshTransform == null) return;

		// Detect if the parent transform was moved (e.g. by gizmo) and shift cloth positions accordingly
		var currentPos = _meshTransform.position;
		var delta = currentPos - _lastMeshWorldPosition;
		if (delta.sqrMagnitude > 1e-8f)
		{
			_cloth.Translate(delta);
			_lastMeshWorldPosition = currentPos;
		}
	}

	private void LateUpdate()
	{
		if (_cloth == null || _clothMesh == null || _meshTransform == null) return;

		var positions = _cloth.GetPositions();
		if (!positions.IsCreated || positions.Length != _clothMesh.vertexCount) return;

		var localPositions = new NativeArray<Vector3>(positions.Length, Allocator.Temp);
		var worldToLocal = _meshTransform.worldToLocalMatrix;
		for (var i = 0; i < positions.Length; i++)
			localPositions[i] = worldToLocal.MultiplyPoint3x4((Vector3)positions[i]);

		_clothMesh.SetVertices(localPositions);
		_clothMesh.RecalculateNormals();
		_clothMesh.RecalculateBounds();
		localPositions.Dispose();

		// Periodic collider re-discovery
		if (_colliderBridge != null && _clothRoot != null && Time.time >= _nextColliderUpdate)
		{
			_nextColliderUpdate = Time.time + _colliderUpdateInterval;
			RefreshSceneColliders();
		}
	}

	private static Transform FindInHierarchy(Transform root, string name)
	{
		if (root.name == name)
			return root;

		foreach (Transform child in root)
		{
			var result = FindInHierarchy(child, name);
			if (result != null) return result;
		}
		return null;
	}

	private Transform ResolveTargetTransform(string target)
	{
		if (string.IsNullOrEmpty(target))
			return null;
		(_, var linkName) = SDF2Unity.GetModelLinkName(target);
		return FindInHierarchy(transform, linkName);
	}

	private Transform[] DiscoverSceneColliders()
	{
		var modelHelper = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
		var modelTransform = modelHelper != null ? modelHelper.transform : _clothRoot;

		// Use mesh bounds expanded by a margin to find nearby colliders
		var meshBounds = _clothMesh.bounds;
		var worldCenter = _meshTransform.TransformPoint(meshBounds.center);
		var worldExtents = Vector3.Scale(meshBounds.extents, _meshTransform.lossyScale);
		worldExtents += Vector3.one * _searchMargin; // Search margin

		var overlapping = UnityEngine.Physics.OverlapBox(worldCenter, worldExtents, _meshTransform.rotation);
		var colliderTransforms = new List<Transform>();

		foreach (var col in overlapping)
		{
			if (col.isTrigger) continue;
			if (col.transform.IsChildOf(modelTransform)) continue;
			if (!col.transform.IsChildOf(_worldRoot) && !col.transform.IsChildOf(_propsRoot)) continue;
			if (!colliderTransforms.Contains(col.transform))
				colliderTransforms.Add(col.transform);
		}

		return colliderTransforms.ToArray();
	}

	private void RefreshSceneColliders()
	{
		var newColliders = DiscoverSceneColliders();
		var newSet = new HashSet<Transform>(newColliders);

		if (!newSet.SetEquals(_currentColliderSet))
		{
			Debug.Log($"ClothPlugin: Colliders updated — {newColliders.Length} scene colliders near cloth");
			_currentColliderSet.Clear();
			_currentColliderSet.UnionWith(newColliders);
			_colliderBridge.UpdateSceneColliders(newColliders);
		}
	}

	private void ConfigureClothFromMesh(SDFormat.Plugin plugin)
	{
		var mesh = _clothMesh;
		var meshTrans = _meshTransform;

		var srcVertices = mesh.vertices;
		var triangles = mesh.triangles;

		// Convert mesh vertices to world space (mesh may have a scaled/rotated parent transform)
		var worldVerts = new float3[srcVertices.Length];
		for (var i = 0; i < srcVertices.Length; i++)
			worldVerts[i] = (float3)meshTrans.TransformPoint(srcVertices[i]);

		// Per-vertex masses — zero mass = pinned/kinematic
		var mass = plugin.GetValue<float>("cloth/simulation/mass", 1.0f);
		var masses = new float[srcVertices.Length];
		for (var i = 0; i < masses.Length; i++) masses[i] = mass;

		// Anchor/pin configuration: pin vertices at extreme position along an axis
		if (plugin.HasElement("cloth/anchor"))
		{
			var axisStr = plugin.GetValue<string>("cloth/anchor/axis", "y").ToLower();
			var sideStr = plugin.GetValue<string>("cloth/anchor/side", "max").ToLower();
			var threshold = plugin.GetValue<float>("cloth/anchor/threshold", 0.01f);

			var axisIndex = axisStr switch { "x" => 0, "y" => 1, "z" => 2, _ => 1 };
			var findMax = sideStr == "max";

			// Find extreme value on the chosen axis
			var extremeValue = findMax ? float.MinValue : float.MaxValue;
			foreach (var v in worldVerts)
			{
				var val = v[axisIndex];
				extremeValue = findMax ? math.max(extremeValue, val) : math.min(extremeValue, val);
			}

			var pinnedCount = 0;
			for (var i = 0; i < worldVerts.Length; i++)
			{
				if (math.abs(worldVerts[i][axisIndex] - extremeValue) <= threshold)
				{
					masses[i] = 0f;
					pinnedCount++;
				}
			}

			StartSummary.AppendLine($"Anchor: axis={axisStr}({axisIndex}), side={sideStr}, " +
				$"threshold={threshold}, pinned {pinnedCount}/{worldVerts.Length} vertices");
		}

		// Build one DistanceConstraint per unique mesh edge
		var stiffness = plugin.GetValue<float>("cloth/constraints/stretching_stiffness", 0.9f);
		var edgeSet = new HashSet<long>();
		var constraints = new List<DistanceConstraint>();
		for (var i = 0; i < triangles.Length; i += 3)
		{
			TryAddEdge(edgeSet, constraints, worldVerts, triangles[i],     triangles[i + 1], stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, triangles[i + 1], triangles[i + 2], stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, triangles[i],     triangles[i + 2], stiffness);
		}

		_cloth.SolverIterations = plugin.GetValue<int>("cloth/solver/iterations", 5);
		_cloth.Damping = plugin.GetValue<float>("cloth/simulation/damping", 0.98f);
		_cloth.Gravity = new float3(
			plugin.GetValue<float>("cloth/simulation/gravity/x", 0f),
			plugin.GetValue<float>("cloth/simulation/gravity/y", -9.81f),
			plugin.GetValue<float>("cloth/simulation/gravity/z", 0f));
		_cloth.ParticleRadius = plugin.GetValue<float>("cloth/collider/particle_radius", 0.005f);

		_cloth.Initialize(worldVerts, masses, constraints.ToArray());

		StartSummary.AppendLine($"Constraints: {constraints.Count} edges, stiffness={stiffness}, " +
			$"damping={_cloth.Damping}, iterations={_cloth.SolverIterations}");
	}

	private static void TryAddEdge(
		HashSet<long> edgeSet, List<DistanceConstraint> constraints,
		float3[] vertices, int a, int b, float stiffness)
	{
		var lo = math.min(a, b);
		var hi = math.max(a, b);
		var key = (long)lo << 32 | (uint)hi;
		if (!edgeSet.Add(key))
			return;

		constraints.Add(new DistanceConstraint
		{
			IndexA = lo,
			IndexB = hi,
			RestLength = math.length(vertices[hi] - vertices[lo]),
			Stiffness = stiffness
		});
	}
}
