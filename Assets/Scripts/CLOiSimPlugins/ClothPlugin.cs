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
	private Transform _linkTransform;

	[SerializeField]
	private Transform _meshTransform;

	[SerializeField]
	private float _searchMargin = 10f;

	[SerializeField]
	private float _colliderUpdateInterval = 1.0f;

	private BoxCollider _clothCollider;
	private float _nextColliderUpdate;
	private float _nextSelectionColliderUpdate;
	private Transform _clothRoot;
	private Transform _worldRoot;
	private Transform _propsRoot;
	private readonly HashSet<Transform> _currentColliderSet = new();
	private Vector3 _lastMeshWorldPosition;
	private ArticulationBody _clothArticulationBody;
	private Rigidbody _clothRigidbody;

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

		var target = pluginParams.GetValue<string>("target");
		_linkTransform = ResolveTargetTransform(target);
		if (_linkTransform == null)
		{
			Debug.LogError($"ClothPlugin: Cannot resolve target link '{target}'");
			yield break;
		}

		_cloth = transform.gameObject.AddComponent<BurstCloth>();

		if (!InitializeClothMesh(target))
			yield break;

		yield return WaitForModelDeployment();
		yield return null;

		_clothRoot = _linkTransform;
		_worldRoot = Main.WorldRoot.transform;
		_propsRoot = Main.PropsRoot.transform;
		_searchMargin = pluginParams.GetValue<float>("cloth/collider/search_margin", _searchMargin);
		_colliderUpdateInterval = pluginParams.GetValue<float>("cloth/collider/update_interval", _colliderUpdateInterval);

		DisableModelPhysics();
		ConfigureClothFromMesh(pluginParams);
		InitializeColliderBridge();

		StartSummary.AppendLine($"Cloth initialized from mesh '{_clothMesh.name}': " +
			$"{_clothMesh.vertexCount} vertices, {_currentColliderSet.Count} scene colliders");

		yield return null;
	}

	private bool InitializeClothMesh(string target)
	{
		var meshFilter = _linkTransform.GetComponentInChildren<MeshFilter>();
		if (meshFilter == null || meshFilter.sharedMesh == null)
		{
			Debug.LogError($"ClothPlugin: No mesh found under target link '{target}'");
			return false;
		}

		_clothMesh = Object.Instantiate(meshFilter.sharedMesh);
		_clothMesh.name = meshFilter.sharedMesh.name + "_cloth";
		_clothMesh.MarkDynamic();
		meshFilter.mesh = _clothMesh;
		_meshTransform = meshFilter.transform;

		// Trigger BoxCollider on the visual GO so gizmo raycast can select via
		// GetComponentInParent<SDFormat.Helper.Link>()
		// Using BoxCollider instead of convex MeshCollider because flat/coplanar
		// cloth meshes cause PhysX QuickHull failures.
		var collider = meshFilter.gameObject.AddComponent<BoxCollider>();
		collider.isTrigger = true;
		var bounds = _clothMesh.bounds;
		collider.center = bounds.center;
		collider.size = bounds.size;
		_clothCollider = collider;

		// Match layer of existing colliders on this model.
		// During deployment, they are on "Ignore Raycast" so our new collider
		// won't intercept the placement raycast. UnblockSelfRaycast() in
		// CompleteDeployment restores all colliders to "Default" afterwards.
		var rootModel = _linkTransform.GetComponentInParent<SDFormat.Helper.Model>();
		var existingCollider = rootModel != null
			? rootModel.GetComponentInChildren<Collider>()
			: null;
		if (existingCollider != null && existingCollider != collider)
			collider.gameObject.layer = existingCollider.gameObject.layer;

		return true;
	}

	private WaitUntil WaitForModelDeployment()
	{
		return new WaitUntil(() =>
		{
			var modelHelper = _linkTransform.GetComponentInParent<SDFormat.Helper.Model>();
			if (modelHelper == null) return true;
			var rootModel = modelHelper.RootModel ?? modelHelper;
			var artBody = rootModel.GetComponentInChildren<ArticulationBody>();
			return artBody == null || !artBody.immovable;
		});
	}

	private void DisableModelPhysics()
	{
		var modelHelper = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
		if (modelHelper == null) return;

		var root = modelHelper.RootModel ?? modelHelper;
		_clothArticulationBody = root.GetComponentInChildren<ArticulationBody>();
		if (_clothArticulationBody != null)
		{
			_clothArticulationBody.immovable = true;
			_clothArticulationBody.useGravity = false;
		}
		else
		{
			_clothRigidbody = root.GetComponentInChildren<Rigidbody>();
			if (_clothRigidbody != null)
			{
				_clothRigidbody.isKinematic = true;
				_clothRigidbody.useGravity = false;
			}
		}
	}

	private void InitializeColliderBridge()
	{
		_colliderBridge = transform.gameObject.AddComponent<ColliderBridge>();
		var sceneColliders = DiscoverSceneColliders();
		_colliderBridge.Initialize(_cloth, sceneColliders);
		_currentColliderSet.UnionWith(sceneColliders);
		_nextColliderUpdate = Time.time + _colliderUpdateInterval;
		_lastMeshWorldPosition = _meshTransform.position;
	}

	protected override void OnReset()
	{
		_cloth?.ResetToInitialState();
	}

	private void Update()
	{
		if (_cloth == null || _meshTransform == null) return;

		// Pause cloth simulation while gizmo is actively transforming this object
		var gizmo = Main.Gizmos;
		_cloth.Paused = gizmo != null && gizmo.isTransforming && IsGizmoTargetingSelf(gizmo);

		// Detect if the parent transform was moved (e.g. by gizmo) and shift cloth positions accordingly
		var currentPos = _meshTransform.position;
		var delta = currentPos - _lastMeshWorldPosition;
		if (delta.sqrMagnitude > 1e-8f)
		{
			_cloth.Translate(delta);
			_lastMeshWorldPosition = currentPos;
		}

		// Re-enforce immovable after gizmo releases (gizmo sets immovable=false on release)
		if (_clothArticulationBody != null && !_clothArticulationBody.immovable)
			_clothArticulationBody.immovable = true;
		else if (_clothRigidbody != null && !_clothRigidbody.isKinematic)
			_clothRigidbody.isKinematic = true;
	}

	private bool IsGizmoTargetingSelf(RuntimeGizmos.TransformGizmo gizmo)
	{
		gizmo.GetSelectedTargets(out var targets);
		if (targets == null) return false;

		for (var i = 0; i < targets.Count; i++)
		{
			if (_meshTransform.IsChildOf(targets[i]))
				return true;
		}
		return false;
	}

	private void LateUpdate()
	{
		if (_cloth == null || _clothMesh == null || _meshTransform == null) return;

		var positions = _cloth.GetPositions();
		if (!positions.IsCreated || positions.Length != _clothMesh.vertexCount) return;

		UpdateClothMesh(positions);
		UpdateSelectionCollider();

		if (_colliderBridge != null && _clothRoot != null && Time.time >= _nextColliderUpdate)
		{
			_nextColliderUpdate = Time.time + _colliderUpdateInterval;
			RefreshSceneColliders();
		}
	}

	private void UpdateClothMesh(NativeArray<float3> positions)
	{
		var localPositions = new NativeArray<Vector3>(positions.Length, Allocator.Temp);
		var worldToLocal = _meshTransform.worldToLocalMatrix;
		for (var i = 0; i < positions.Length; i++)
			localPositions[i] = worldToLocal.MultiplyPoint3x4((Vector3)positions[i]);

		_clothMesh.SetVertices(localPositions);
		_clothMesh.RecalculateNormals();
		_clothMesh.RecalculateBounds();
		localPositions.Dispose();
	}

	private void UpdateSelectionCollider()
	{
		if (_clothCollider == null || Time.time < _nextSelectionColliderUpdate) return;

		_nextSelectionColliderUpdate = Time.time + 0.5f;
		var bounds = _clothMesh.bounds;
		_clothCollider.center = bounds.center;
		_clothCollider.size = bounds.size;
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
		var worldVerts = ConvertVerticesToWorldSpace();
		var masses = ComputeVertexMasses(plugin, worldVerts.Length);
		ApplyAnchorPins(plugin, worldVerts, masses);

		var weldConstraints = BuildWeldConstraints(worldVerts, masses, out var weldCount);
		BuildEdgeAndBendingConstraints(plugin, worldVerts, _clothMesh.triangles, weldConstraints,
			out var constraints, out var bendingConstraints);

		ApplyClothParameters(plugin);
		_cloth.Initialize(worldVerts, masses, constraints, bendingConstraints);

		StartSummary.AppendLine(
			$"Constraints: {constraints.Length} stretch ({weldCount} weld) + {bendingConstraints.Length} bending, " +
			$"stiffness={plugin.GetValue<float>("cloth/constraints/stretching_stiffness", 0.9f)}/" +
			$"{plugin.GetValue<float>("cloth/constraints/bending_stiffness", 0.3f)}, " +
			$"damping={_cloth.Damping}, iterations={_cloth.SolverIterations}");
	}

	private float3[] ConvertVerticesToWorldSpace()
	{
		var srcVertices = _clothMesh.vertices;
		var worldVerts = new float3[srcVertices.Length];
		for (var i = 0; i < srcVertices.Length; i++)
			worldVerts[i] = (float3)_meshTransform.TransformPoint(srcVertices[i]);
		return worldVerts;
	}

	private float[] ComputeVertexMasses(SDFormat.Plugin plugin, int vertexCount)
	{
		var totalMass = 0f;
		if (_clothArticulationBody != null)
			totalMass = _clothArticulationBody.mass;
		else if (_clothRigidbody != null)
			totalMass = _clothRigidbody.mass;

		if (totalMass <= 0f)
			totalMass = plugin.GetValue<float>("cloth/simulation/mass", 1.0f);

		var perVertexMass = totalMass / vertexCount;
		var masses = new float[vertexCount];
		for (var i = 0; i < masses.Length; i++) masses[i] = perVertexMass;
		return masses;
	}

	private void ApplyAnchorPins(SDFormat.Plugin plugin, float3[] worldVerts, float[] masses)
	{
		if (!plugin.HasElement("cloth/anchor")) return;

		var axisStr = plugin.GetValue<string>("cloth/anchor/axis", "y").ToLower();
		var sideStr = plugin.GetValue<string>("cloth/anchor/side", "max").ToLower();
		var threshold = plugin.GetValue<float>("cloth/anchor/threshold", 0.01f);

		var axisIndex = axisStr switch { "x" => 0, "y" => 1, "z" => 2, _ => 1 };
		var findMax = sideStr == "max";

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

	private static List<DistanceConstraint> BuildWeldConstraints(float3[] worldVerts, float[] masses, out int weldCount)
	{
		weldCount = 0;
		var weldThreshold = 1e-6f;
		var weldConstraints = new List<DistanceConstraint>();
		var bucketSize = 0.001f;
		var buckets = new Dictionary<long, List<int>>();

		for (var i = 0; i < worldVerts.Length; i++)
		{
			var bx = (long)math.floor(worldVerts[i].x / bucketSize);
			var by = (long)math.floor(worldVerts[i].y / bucketSize);
			var bz = (long)math.floor(worldVerts[i].z / bucketSize);
			var bucketKey = bx * 73856093L ^ by * 19349663L ^ bz * 83492791L;

			if (!buckets.TryGetValue(bucketKey, out var bucket))
			{
				bucket = new List<int>();
				buckets[bucketKey] = bucket;
			}

			foreach (var j in bucket)
			{
				if (math.lengthsq(worldVerts[i] - worldVerts[j]) < weldThreshold)
				{
					weldConstraints.Add(new DistanceConstraint
					{
						IndexA = math.min(i, j),
						IndexB = math.max(i, j),
						RestLength = 0f,
						Stiffness = 1.0f
					});
					if (masses[i] == 0f && masses[j] != 0f) masses[j] = 0f;
					else if (masses[j] == 0f && masses[i] != 0f) masses[i] = 0f;
					weldCount++;
				}
			}
			bucket.Add(i);
		}

		return weldConstraints;
	}

	private static void BuildEdgeAndBendingConstraints(
		SDFormat.Plugin plugin, float3[] worldVerts, int[] triangles,
		List<DistanceConstraint> weldConstraints,
		out DistanceConstraint[] outConstraints, out BendingConstraint[] outBendingConstraints)
	{
		var stiffness = plugin.GetValue<float>("cloth/constraints/stretching_stiffness", 0.9f);
		var bendingStiffness = plugin.GetValue<float>("cloth/constraints/bending_stiffness", 0.3f);
		var edgeSet = new HashSet<long>();
		var constraints = new List<DistanceConstraint>(weldConstraints);
		var edgeToTriOpposite = new Dictionary<long, int>();
		var bendingConstraints = new List<BendingConstraint>();

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var i0 = triangles[i];
			var i1 = triangles[i + 1];
			var i2 = triangles[i + 2];

			TryAddEdge(edgeSet, constraints, worldVerts, i0, i1, stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, i1, i2, stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, i0, i2, stiffness);

			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i0, i1, i2, bendingStiffness);
			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i1, i2, i0, bendingStiffness);
			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i0, i2, i1, bendingStiffness);
		}

		outConstraints = constraints.ToArray();
		outBendingConstraints = bendingConstraints.ToArray();
	}

	private void ApplyClothParameters(SDFormat.Plugin plugin)
	{
		_cloth.SolverIterations = plugin.GetValue<int>("cloth/solver/iterations", 5);
		_cloth.Damping = plugin.GetValue<float>("cloth/simulation/damping", 0.98f);
		_cloth.Gravity = new float3(
			plugin.GetValue<float>("cloth/simulation/gravity/x", 0f),
			plugin.GetValue<float>("cloth/simulation/gravity/y", -9.81f),
			plugin.GetValue<float>("cloth/simulation/gravity/z", 0f));
		_cloth.ParticleRadius = plugin.GetValue<float>("cloth/collider/particle_radius", 0.01f);
		_cloth.SubSteps = plugin.GetValue<int>("cloth/solver/sub_steps", 4);
		_cloth.Friction = plugin.GetValue<float>("cloth/simulation/friction", 0.5f);
		_cloth.SleepThreshold = plugin.GetValue<float>("cloth/simulation/sleep_threshold", 0.001f);
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

	/// <summary>
	/// For edge (edgeA, edgeB) with opposite vertex 'opposite' in this triangle,
	/// if we've already seen this edge from another triangle, create a bending constraint
	/// between the two opposite vertices.
	/// </summary>
	private static void TryAddBending(
		Dictionary<long, int> edgeToOpposite, List<BendingConstraint> bendingConstraints,
		float3[] vertices, int edgeA, int edgeB, int opposite, float stiffness)
	{
		var lo = math.min(edgeA, edgeB);
		var hi = math.max(edgeA, edgeB);
		var key = (long)lo << 32 | (uint)hi;

		if (edgeToOpposite.TryGetValue(key, out var otherOpposite))
		{
			// Second triangle sharing this edge — create bending constraint
			bendingConstraints.Add(new BendingConstraint
			{
				IndexA = otherOpposite,
				IndexB = opposite,
				RestLength = math.length(vertices[opposite] - vertices[otherOpposite]),
				Stiffness = stiffness
			});
		}
		else
		{
			// First triangle with this edge — store opposite vertex
			edgeToOpposite[key] = opposite;
		}
	}
}
